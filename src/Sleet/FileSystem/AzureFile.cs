using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using NuGet.Logging;

namespace Sleet
{
    public class AzureFile : FileBase
    {
        private readonly CloudBlockBlob _blob;

        internal AzureFile(AzureFileSystem fileSystem, Uri rootPath, Uri displayPath, FileInfo localCacheFile, CloudBlockBlob blob)
            : base(fileSystem, rootPath, displayPath, localCacheFile)
        {
            _blob = blob;
        }

        protected override async Task CopyFromSource(ILogger log, CancellationToken token)
        {
            if (await _blob.ExistsAsync())
            {
                log.LogInformation($"GET {_blob.Uri.AbsoluteUri}");

                if (File.Exists(LocalCacheFile.FullName))
                {
                    LocalCacheFile.Delete();
                }

                using (var cache = File.OpenWrite(LocalCacheFile.FullName))
                {
                    await _blob.DownloadToStreamAsync(cache);
                }

                // If the blob is compressed it needs to be decompressed locally before it can be used
                if (_blob.Properties.ContentEncoding?.Equals("gzip", StringComparison.OrdinalIgnoreCase) == true)
                {
                    log.LogInformation($"Decompressing {_blob.Uri.AbsoluteUri}");

                    var gzipFile = LocalCacheFile.FullName + ".gz";
                    File.Move(LocalCacheFile.FullName, gzipFile);

                    using (Stream destination = File.Create(LocalCacheFile.FullName))
                    using (Stream source = File.OpenRead(gzipFile))
                    using (Stream zipStream = new GZipStream(source, CompressionMode.Decompress))
                    {
                        zipStream.CopyTo(destination);
                    }
                }
            }
        }

        protected override async Task CopyToSource(ILogger log, CancellationToken token)
        {
            if (File.Exists(LocalCacheFile.FullName))
            {
                log.LogInformation($"Pushing {_blob.Uri.AbsoluteUri}");

                using (var cache = LocalCacheFile.OpenRead())
                {
                    Stream writeStream = cache;

                    if (_blob.Uri.AbsoluteUri.EndsWith(".json", StringComparison.Ordinal))
                    {
                        _blob.Properties.ContentType = "application/json";
                        _blob.Properties.ContentEncoding = "gzip";

                        // Compress content before uploading
                        log.LogInformation($"Compressing {_blob.Uri.AbsoluteUri}");
                        writeStream = GZipAndMinify(cache);
                    }
                    else if (_blob.Uri.AbsoluteUri.EndsWith(".nupkg", StringComparison.Ordinal))
                    {
                        _blob.Properties.ContentType = "application/zip";
                    }
                    else if (_blob.Uri.AbsoluteUri.EndsWith(".xml", StringComparison.Ordinal))
                    {
                        _blob.Properties.ContentType = "application/xml";
                    }

                    await _blob.UploadFromStreamAsync(writeStream);

                    writeStream.Dispose();
                }

                _blob.Properties.CacheControl = "no-store";

                // TODO: re-enable this once it works again. 
                _blob.Properties.ContentMD5 = null;

                await _blob.SetPropertiesAsync();
            }
            else if (await _blob.ExistsAsync())
            {
                log.LogInformation($"Removing {_blob.Uri.AbsoluteUri}");
                await _blob.DeleteAsync();
            }
            else
            {
                log.LogInformation($"Skipping {_blob.Uri.AbsoluteUri}");
            }
        }

        /// <summary>
        /// Compress and remove indentation for json data
        /// </summary>
        private static MemoryStream GZipAndMinify(FileStream input)
        {
            var memoryStream = new MemoryStream();

            var json = JsonUtility.LoadJson(input);

            using (GZipStream zipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
            using (var writer = new StreamWriter(zipStream, Encoding.UTF8))
            {
                writer.Write(json.ToString(Formatting.None));

                writer.Flush();
                zipStream.Flush();
                memoryStream.Flush();
            }

            memoryStream.Seek(0, SeekOrigin.Begin);

            return memoryStream;
        }
    }
}
