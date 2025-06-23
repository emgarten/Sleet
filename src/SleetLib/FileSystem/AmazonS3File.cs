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
        private readonly bool compress = true;
        private readonly ServerSideEncryptionMethod serverSideEncryptionMethod;
        private readonly S3CannedACL acl;
        private readonly bool disablePayloadSigning;

        internal AmazonS3File(
            AmazonS3FileSystem fileSystem,
            Uri rootPath,
            Uri displayPath,
            FileInfo localCacheFile,
            IAmazonS3 client,
            string bucketName,
            string key,
            ServerSideEncryptionMethod serverSideEncryptionMethod,
            bool compress = true,
            S3CannedACL acl = null,
            bool disablePayloadSigning = false)
            : base(fileSystem, rootPath, displayPath, localCacheFile, fileSystem.LocalCache.PerfTracker)
        {
            this.client = client;
            this.bucketName = bucketName;
            this.key = key;
            this.compress = compress;
            this.serverSideEncryptionMethod = serverSideEncryptionMethod;
            this.acl = acl;
            this.disablePayloadSigning = disablePayloadSigning;
        }

        protected override async Task CopyFromSource(ILogger log, CancellationToken token)
        {
            var absoluteUri = UriUtility.GetPath(RootPath, key);
            if (!await FileExistsAsync(client, bucketName, key, token).ConfigureAwait(false))
                return;

            log.LogVerbose($"GET {absoluteUri}");

            DeleteInternal();

            string contentEncoding;
            using (var cache = File.OpenWrite(LocalCacheFile.FullName))
            {
                contentEncoding = await DownloadFileAsync(client, bucketName, key, cache, token).ConfigureAwait(false);
            }

            if (contentEncoding?.Equals("gzip", StringComparison.OrdinalIgnoreCase) == true)
            {
                log.LogVerbose($"Decompressing {absoluteUri}");

                var gzipFile = LocalCacheFile.FullName + ".gz";
                File.Move(LocalCacheFile.FullName, gzipFile);

                using (Stream destination = File.Create(LocalCacheFile.FullName))
                using (Stream source = File.OpenRead(gzipFile))
                using (Stream zipStream = new GZipStream(source, CompressionMode.Decompress))
                {
                    await zipStream.CopyToAsync(destination, DefaultCopyBufferSize, token).ConfigureAwait(false);
                }

                File.Delete(gzipFile);
            }
        }

        protected override async Task CopyToSource(ILogger log, CancellationToken token)
        {
            var absoluteUri = UriUtility.GetPath(RootPath, key);
            if (!File.Exists(LocalCacheFile.FullName))
            {
                if (await FileExistsAsync(client, bucketName, key, token).ConfigureAwait(false))
                {
                    log.LogVerbose($"Removing {absoluteUri}");
                    await RemoveFileAsync(client, bucketName, key, token).ConfigureAwait(false);
                }
                else
                {
                    log.LogVerbose($"Skipping {absoluteUri}");
                }

                return;
            }

            log.LogVerbose($"Pushing {absoluteUri}");

            using (var cache = LocalCacheFile.OpenRead())
            {
                Stream writeStream = cache;
                string contentType = null, contentEncoding = null;
                bool disposeWriteStream = false;
                
                if (key.EndsWith(".nupkg", StringComparison.Ordinal))
                {
                    contentType = "application/zip";
                }
                else if (key.EndsWith(".xml", StringComparison.Ordinal)
                         || key.EndsWith(".nuspec", StringComparison.Ordinal))
                {
                    contentType = "application/xml";
                }
                else if (key.EndsWith(".svg", StringComparison.Ordinal))
                {
                    contentType = "image/svg+xml";
                }
                else if (key.EndsWith(".json", StringComparison.Ordinal)
                         || await JsonUtility.IsJsonAsync(LocalCacheFile.FullName))
                {
                    contentType = "application/json";
                    if (compress && !SkipCompress())
                    {
                        contentEncoding = "gzip";
                        writeStream = await JsonUtility.GZipAndMinifyAsync(cache);
                        disposeWriteStream = true;
                    }
                }
                else if (key.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                         || key.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = "application/octet-stream";
                }
                else if (absoluteUri.AbsoluteUri.EndsWith("/icon"))
                {
                    contentType = "image/png";
                }
                else if (absoluteUri.AbsoluteUri.EndsWith("/readme"))
                {
                    contentType = "text/markdown";
                }
                else
                {
                    log.LogWarning($"Unknown file type: {absoluteUri}");
                }

                try
                {
                    await UploadFileAsync(client, bucketName, key, contentType, contentEncoding, writeStream, serverSideEncryptionMethod, acl, disablePayloadSigning, token)
                        .ConfigureAwait(false);
                }
                finally
                {
                    if (disposeWriteStream && writeStream != cache)
                    {
                        writeStream.Dispose();
                    }
                }
            }
        }

        protected override Task<bool> RemoteExists(ILogger log, CancellationToken token)
        {
            return FileExistsAsync(client, bucketName, key, token);
        }
    }
}
