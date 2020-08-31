using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Storage.Blob;
using NuGet.Common;

namespace Sleet
{
    public class AzureFile : FileBase
    {
        private readonly CloudBlockBlob _blob;

        internal AzureFile(AzureFileSystem fileSystem, Uri rootPath, Uri displayPath, FileInfo localCacheFile, CloudBlockBlob blob)
            : base(fileSystem, rootPath, displayPath, localCacheFile, fileSystem.LocalCache.PerfTracker)
        {
            _blob = blob;
        }

        protected override async Task CopyFromSource(ILogger log, CancellationToken token)
        {
            if (await _blob.ExistsAsync())
            {
                log.LogVerbose($"GET {_blob.Uri.AbsoluteUri}");

                DeleteInternal();

                using (var cache = File.OpenWrite(LocalCacheFile.FullName))
                {
                    await _blob.DownloadToStreamAsync(cache);
                }

                // If the blob is compressed it needs to be decompressed locally before it can be used
                if (_blob.Properties.ContentEncoding?.Equals("gzip", StringComparison.OrdinalIgnoreCase) == true)
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

                    if (_blob.Uri.AbsoluteUri.EndsWith(".nupkg", StringComparison.Ordinal))
                    {
                        _blob.Properties.ContentType = "application/zip";
                    }
                    else if (_blob.Uri.AbsoluteUri.EndsWith(".xml", StringComparison.Ordinal)
                        || _blob.Uri.AbsoluteUri.EndsWith(".nuspec", StringComparison.Ordinal))
                    {
                        _blob.Properties.ContentType = "application/xml";
                    }
                    else if (_blob.Uri.AbsoluteUri.EndsWith(".svg", StringComparison.Ordinal))
                    {
                        _blob.Properties.ContentType = "image/svg+xml";
                    }
                    else if (_blob.Uri.AbsoluteUri.EndsWith(".json", StringComparison.Ordinal)
                            || await JsonUtility.IsJsonAsync(LocalCacheFile.FullName))
                    {
                        _blob.Properties.ContentType = "application/json";
                        _blob.Properties.ContentEncoding = "gzip";

                        // Compress content before uploading
                        log.LogVerbose($"Compressing {_blob.Uri.AbsoluteUri}");
                        writeStream = await JsonUtility.GZipAndMinifyAsync(cache);
                    }
                    else if (_blob.Uri.AbsoluteUri.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                        || _blob.Uri.AbsoluteUri.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                    {
                        _blob.Properties.ContentType = "application/octet-stream";
                    }
                    else
                    {
                        log.LogWarning($"Unknown file type: {_blob.Uri.AbsoluteUri}");
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
                log.LogVerbose($"Removing {_blob.Uri.AbsoluteUri}");
                await _blob.DeleteAsync();
            }
            else
            {
                log.LogVerbose($"Skipping {_blob.Uri.AbsoluteUri}");
            }
        }

        protected override Task<bool> RemoteExists(ILogger log, CancellationToken token)
        {
            return _blob.ExistsAsync();
        }
    }
}