using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Common;

namespace Sleet
{
    public class AzureFileSystem : ISleetFileSystem
    {
        private readonly Uri _root;
        private readonly Uri _baseUri;
        private readonly LocalCache _cache;
        private readonly ConcurrentDictionary<Uri, ISleetFile> _files;
        private readonly CloudStorageAccount _azureAccount;
        private readonly CloudBlobClient _client;
        private readonly CloudBlobContainer _container;

        public AzureFileSystem(LocalCache cache, Uri root, CloudStorageAccount azureAccount, string container)
            : this(cache, root, root, azureAccount, container)
        {
        }

        public AzureFileSystem(LocalCache cache, Uri root, Uri baseUri, CloudStorageAccount azureAccount, string container)
        {
            _baseUri = UriUtility.AddTrailingSlash(baseUri);
            _root = UriUtility.AddTrailingSlash(root);
            _cache = cache;
            _files = new ConcurrentDictionary<Uri, ISleetFile>();

            _azureAccount = azureAccount;
            _client = _azureAccount.CreateCloudBlobClient();
            _container = _client.GetContainerReference(container);
        }

        public ConcurrentDictionary<Uri, ISleetFile> Files
        {
            get
            {
                return _files;
            }
        }

        public LocalCache LocalCache
        {
            get
            {
                return _cache;
            }
        }

        public Uri BaseURI
        {
            get
            {
                return _root;
            }
        }

        public ISleetFile Get(string relativePath)
        {
            return Get(GetPath(relativePath));
        }

        public ISleetFile Get(Uri path)
        {
            var relativePath = GetRelativePath(path);

            var blob = _container.GetBlockBlobReference(relativePath);

            var file = Files.GetOrAdd(path, (uri) =>
                {
                    var rootUri = UriUtility.ChangeRoot(_baseUri, _root, uri);

                    return new AzureFile(
                        this,
                        rootUri,
                        uri,
                        LocalCache.GetNewTempPath(),
                        blob);
                });

            return file;
        }

        public Uri GetPath(string relativePath)
        {
            return UriUtility.GetPath(BaseURI, relativePath);
        }

        public async Task<bool> Commit(ILogger log, CancellationToken token)
        {
            foreach (var file in Files.Values)
            {
                await file.Push(log, token);
            }

            return true;
        }

        public string GetRelativePath(Uri uri)
        {
            return uri.AbsoluteUri.Replace(_root.AbsoluteUri, string.Empty);
        }

        public async Task<bool> Validate(ILogger log, CancellationToken token)
        {
            log.LogInformation($"Verifying {_container.Uri.AbsoluteUri} exists.");

            if (await _container.ExistsAsync())
            {
                log.LogInformation($"Found {_container.Uri.AbsoluteUri}");
            }
            else
            {
                log.LogError($"Unable to find {_container.Uri.AbsoluteUri}. Verify that the storage account and container exists. The container must be created manually before using this feed.");
                return false;
            }

            return true;
        }

        public ISleetFileSystemLock CreateLock(ILogger log)
        {
            return new AzureFileSystemLock(_container, log);
        }
    }
}
