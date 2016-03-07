using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Logging;

namespace Sleet
{
    public class AzureFile : FileBase
    {
        private readonly CloudBlockBlob _blob;

        internal AzureFile(AzureFileSystem fileSystem, Uri path, FileInfo localCacheFile, CloudBlockBlob blob)
            : base(fileSystem, path, localCacheFile)
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
            }
        }

        protected override async Task CopyToSource(ILogger log, CancellationToken token)
        {
            if (File.Exists(LocalCacheFile.FullName))
            {
                log.LogInformation($"Pushing {_blob.Uri.AbsoluteUri}");

                using (var cache = LocalCacheFile.OpenRead())
                {
                    if (_blob.Uri.AbsoluteUri.EndsWith(".json", StringComparison.Ordinal))
                    {
                        _blob.Properties.ContentType = "application/json";
                    }
                    else if (_blob.Uri.AbsoluteUri.EndsWith(".nupkg", StringComparison.Ordinal))
                    {
                        _blob.Properties.ContentType = "application/zip";
                    }

                    await _blob.UploadFromStreamAsync(cache);
                }

                _blob.Properties.CacheControl = "no-store";
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
    }
}
