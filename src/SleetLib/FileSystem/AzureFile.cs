using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using NuGet.Common;
using System.IO.Compression;

namespace Sleet
{
    public class AzureFile : FileBase
    {
        private readonly BlobClient _blob;

        internal AzureFile(AzureFileSystem fileSystem, Uri rootPath, Uri displayPath, FileInfo localCacheFile, BlobClient blob)
            : base(fileSystem, rootPath, displayPath, localCacheFile, fileSystem.LocalCache.PerfTracker)
        {
            _blob = blob;
        }

        protected override async Task CopyFromSource(ILogger log, CancellationToken token)
        {
            if (await _blob.ExistsAsync(token))
            {
                log.LogVerbose($"GET {_blob.Uri.AbsoluteUri}");

                DeleteInternal();

                using (var cache = File.OpenWrite(LocalCacheFile.FullName))
                {
                    await _blob.DownloadToAsync(cache, token);
                }

                // If the blob is compressed it needs to be decompressed locally before it can be used
                var blobProperties = await _blob.GetPropertiesAsync(cancellationToken: token);
                if (blobProperties.Value.ContentEncoding != null && blobProperties.Value.ContentEncoding.Equals("gzip", StringComparison.OrdinalIgnoreCase))
                {
                    log.LogVerbose($"Decompressing {_blob.Uri.AbsoluteUri}");

                    var gzipFile = LocalCacheFile.FullName + ".gz";
                    File.Move(LocalCacheFile.FullName, gzipFile);

                    using (Stream destination = File.Create(LocalCacheFile.FullName))
                    using (Stream source = File.OpenRead(gzipFile))
                    using (Stream zipStream = new GZipStream(source, CompressionMode.Decompress))
                    {
                        await zipStream.CopyToAsync(destination);
                    }

                    File.Delete(gzipFile);
                }
            }
        }

        protected override async Task CopyToSource(ILogger log, CancellationToken token)
        {
            if (File.Exists(LocalCacheFile.FullName))
            {
                log.LogVerbose($"Pushing {_blob.Uri.AbsoluteUri}");

                using (var cache = LocalCacheFile.OpenRead())
                {
                    Stream writeStream = cache;
                    var blobHeaders = new BlobHttpHeaders
                    {
                        CacheControl = "no-store"
                    };

                    if (_blob.Uri.AbsoluteUri.EndsWith(".nupkg", StringComparison.Ordinal))
                    {
                        blobHeaders.ContentType = "application/zip";
                    }
                    else if (_blob.Uri.AbsoluteUri.EndsWith(".xml", StringComparison.Ordinal)
                        || _blob.Uri.AbsoluteUri.EndsWith(".nuspec", StringComparison.Ordinal))
                    {
                        blobHeaders.ContentType = "application/xml";
                    }
                    else if (_blob.Uri.AbsoluteUri.EndsWith(".svg", StringComparison.Ordinal))
                    {
                        blobHeaders.ContentType = "image/svg+xml";
                    }
                    else if (_blob.Uri.AbsoluteUri.EndsWith(".json", StringComparison.Ordinal)
                            || await JsonUtility.IsJsonAsync(LocalCacheFile.FullName))
                    {
                        blobHeaders.ContentType = "application/json";

                        if (!SkipCompress())
                        {
                            blobHeaders.ContentEncoding = "gzip";
                            writeStream = await JsonUtility.GZipAndMinifyAsync(cache);
                        }
                    }
                    else if (_blob.Uri.AbsoluteUri.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                        || _blob.Uri.AbsoluteUri.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                    {
                        blobHeaders.ContentType = "application/octet-stream";
                    }
                    else if (_blob.Uri.AbsoluteUri.EndsWith("/icon"))
                    {
                        blobHeaders.ContentType = "image/png";
                    }
                    else if (_blob.Uri.AbsoluteUri.EndsWith("/readme"))
                    {
                        blobHeaders.ContentType = "text/markdown";
                    }
                    else
                    {
                        log.LogWarning($"Unknown file type: {_blob.Uri.AbsoluteUri}");
                    }

                    await _blob.UploadAsync(writeStream, blobHeaders, cancellationToken: token);

                    writeStream.Dispose();
                }
            }
            else if (await _blob.ExistsAsync(token))
            {
                log.LogVerbose($"Removing {_blob.Uri.AbsoluteUri}");
                await _blob.DeleteAsync(cancellationToken: token);
            }
            else
            {
                log.LogVerbose($"Skipping {_blob.Uri.AbsoluteUri}");
            }
        }

        protected override async Task<bool> RemoteExists(ILogger log, CancellationToken token)
        {
            return await _blob.ExistsAsync(token);
        }
    }
}
