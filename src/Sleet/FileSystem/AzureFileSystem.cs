using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Logging;

namespace Sleet
{
    public class AzureFileSystem : ISleetFileSystem
    {
        private readonly Uri _root;
        private readonly LocalCache _cache;
        private readonly ConcurrentDictionary<Uri, ISleetFile> _files;
        private readonly CloudStorageAccount _azureAccount;
        private readonly CloudBlobClient _client;
        private readonly CloudBlobContainer _container;

        public AzureFileSystem(LocalCache cache, Uri root, CloudStorageAccount azureAccount, string container)
        {
            _root = new Uri(root.AbsoluteUri.TrimEnd('/') + '/');
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

        public Uri Root
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

            var file = Files.GetOrAdd(path, (uri) => new AzureFile(
                this,
                uri,
                LocalCache.GetNewTempPath(),
                blob));

            return file;
        }

        public Uri GetPath(string relativePath)
        {
            var combined = new Uri(Root, relativePath);
            return combined;
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
    }
}
