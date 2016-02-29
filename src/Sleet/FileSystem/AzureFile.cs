using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Logging;

namespace Sleet
{
    public class AzureFile : ISleetFile
    {
        private readonly AzureFileSystem _fileSystem;
        private readonly Uri _path;
        private readonly FileInfo _localCacheFile;
        private readonly CloudBlockBlob _blob;
        private bool _isLoaded;

        internal AzureFile(AzureFileSystem fileSystem, Uri path, FileInfo localCacheFile, CloudBlockBlob blob)
        {
            _fileSystem = fileSystem;
            _path = path;
            _localCacheFile = localCacheFile;
            _blob = blob;
        }

        public ISleetFileSystem FileSystem
        {
            get
            {
                return _fileSystem;
            }
        }

        public Uri Path
        {
            get
            {
                return _path;
            }
        }

        public async Task<bool> Exists(ILogger log, CancellationToken token)
        {
            return await _blob.ExistsAsync();
        }

        public async Task Get(ILogger log, CancellationToken token)
        {
            if (!_isLoaded)
            {
                if (await _blob.ExistsAsync())
                {
                    log.LogInformation($"GET {_blob.Uri.AbsoluteUri}");

                    if (File.Exists(_localCacheFile.FullName))
                    {
                        _localCacheFile.Delete();
                    }

                    using (var cache = _localCacheFile.OpenWrite())
                    {
                        await _blob.DownloadToStreamAsync(cache);
                    }
                }

                _isLoaded = true;
            }
        }

        public async Task<FileInfo> GetLocal(ILogger log, CancellationToken token)
        {
            await Get(log, token);
            return _localCacheFile;
        }

        public async Task Push(ILogger log, CancellationToken token)
        {
            if (File.Exists(_localCacheFile.FullName))
            {
                log.LogInformation($"Pushing {_blob.Uri.AbsoluteUri}");

                using (var cache = _localCacheFile.OpenRead())
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
