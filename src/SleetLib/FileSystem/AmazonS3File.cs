using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using NuGet.Common;
using static Sleet.AmazonS3FileSystemAbstraction;

namespace Sleet
{
    public class AmazonS3File : FileBase
    {
        private readonly IAmazonS3 client;
        private readonly string bucketName;
        private readonly string key;

        internal AmazonS3File(
            AmazonS3FileSystem fileSystem,
            Uri rootPath,
            Uri displayPath,
            FileInfo localCacheFile,
            IAmazonS3 client,
            string bucketName,
            string key)
            : base(fileSystem, rootPath, displayPath, localCacheFile)
        {
            this.client = client;
            this.bucketName = bucketName;
            this.key = key;
        }

        protected override async Task CopyFromSource(ILogger log, CancellationToken token)
        {
            Uri absoluteUri = UriUtility.GetPath(RootPath, key);
            if (!await FileExistsAsync(client, bucketName, key, token).ConfigureAwait(false))
                return;

            log.LogInformation($"GET {absoluteUri}");

            if (File.Exists(LocalCacheFile.FullName))
                LocalCacheFile.Delete();

            string contentEncoding;
            using (FileStream cache = File.OpenWrite(LocalCacheFile.FullName))
            {
                contentEncoding = await DownloadFileAsync(client, bucketName, key, cache, token).ConfigureAwait(false);
            }

            if (contentEncoding?.Equals("gzip", StringComparison.OrdinalIgnoreCase) == true)
            {
                log.LogInformation($"Decompressing {absoluteUri}");

                string gzipFile = LocalCacheFile.FullName + ".gz";
                File.Move(LocalCacheFile.FullName, gzipFile);

                using (Stream destination = File.Create(LocalCacheFile.FullName))
                using (Stream source = File.OpenRead(gzipFile))
                using (Stream zipStream = new GZipStream(source, CompressionMode.Decompress))
                {
                    await zipStream.CopyToAsync(destination, DefaultCopyBufferSize, token).ConfigureAwait(false);
                }
            }
        }

        protected override async Task CopyToSource(ILogger log, CancellationToken token)
        {
            Uri absoluteUri = UriUtility.GetPath(RootPath, key);
            if (!File.Exists(LocalCacheFile.FullName))
            {
                if (await FileExistsAsync(client, bucketName, key, token).ConfigureAwait(false))
                {
                    log.LogInformation($"Removing {absoluteUri}");
                    await RemoveFileAsync(client, bucketName, key, token).ConfigureAwait(false);
                }
                else
                {
                    log.LogInformation($"Skipping {absoluteUri}");
                }

                return;
            }

            log.LogInformation($"Pushing {absoluteUri}");

            using (FileStream cache = LocalCacheFile.OpenRead())
            {
                Stream writeStream = cache;
                string contentType = null, contentEncoding = null;
                if (key.EndsWith(".nupkg", StringComparison.Ordinal))
                {
                    contentType = "application/zip";
                }
                else if (key.EndsWith(".xml", StringComparison.Ordinal)
                         || key.EndsWith(".nuspec", StringComparison.Ordinal))
                {
                    contentType = "application/xml";
                }
                else if (key.EndsWith(".json", StringComparison.Ordinal)
                         || await JsonUtility.IsJsonAsync(LocalCacheFile.FullName))
                {
                    contentType = "application/json";
                    contentEncoding = "gzip";

                    // Compress content before uploading
                    log.LogInformation($"Compressing {absoluteUri}");
                    writeStream = await JsonUtility.GZipAndMinifyAsync(cache);
                }
                else if (key.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                         || key.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = "application/octet-stream";
                }
                else
                {
                    log.LogWarning($"Unknown file type: {absoluteUri}");
                }

                await UploadFileAsync(client, bucketName, key, contentType, contentEncoding, writeStream, token)
                    .ConfigureAwait(false);

                writeStream.Dispose();
            }
        }

        protected override Task<bool> RemoteExists(ILogger log, CancellationToken token)
        {
            return FileExistsAsync(client, bucketName, key, token);
        }
    }
}