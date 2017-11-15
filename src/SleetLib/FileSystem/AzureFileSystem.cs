using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Common;

namespace Sleet
{
    public class AzureFileSystem : FileSystemBase
    {
        public static readonly string AzureEmptyConnectionString = "DefaultEndpointsProtocol=https;AccountName=;AccountKey=;BlobEndpoint=";

        private readonly CloudStorageAccount _azureAccount;
        private readonly CloudBlobClient _client;
        private readonly CloudBlobContainer _container;
        private readonly string _containerRoot;

        public AzureFileSystem(LocalCache cache, Uri root, CloudStorageAccount azureAccount, string container)
            : this(cache, root, root, azureAccount, container)
        {
        }

        public AzureFileSystem(LocalCache cache, Uri root, Uri baseUri, CloudStorageAccount azureAccount, string container, string feedSubPath = null)
            : base(cache, root, baseUri, feedSubPath)
        {
            _azureAccount = azureAccount;
            _client = _azureAccount.CreateCloudBlobClient();
            _container = _client.GetContainerReference(container);
            _containerRoot = UriUtility.EnsureTrailingSlash(_container.Uri).AbsoluteUri;
        }

        public override ISleetFile Get(Uri path)
        {
            var relativePath = GetPathRelativeToContainer(path);

            var blob = _container.GetBlockBlobReference(relativePath);

            var file = Files.GetOrAdd(path, (uri) =>
                {
                    var rootUri = UriUtility.ChangeRoot(BaseURI, Root, uri);

                    return new AzureFile(
                        this,
                        rootUri,
                        uri,
                        LocalCache.GetNewTempPath(),
                        blob);
                });

            return file;
        }

        public override async Task<bool> Validate(ILogger log, CancellationToken token)
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

        public override ISleetFileSystemLock CreateLock(ILogger log)
        {
            var relativePath = GetPathRelativeToContainer(GetPath(AzureFileSystemLock.LockFile));
            var blob = _container.GetBlockBlobReference(relativePath);
            return new AzureFileSystemLock(blob, log);
        }

        public override async Task<IReadOnlyList<ISleetFile>> GetFiles(ILogger log, CancellationToken token)
        {
            BlobContinuationToken continuationToken = null;
            string prefix = null;
            var useFlatBlobListing = true;
            var blobListingDetails = BlobListingDetails.All;
            int? maxResults = null;

            // Return all files except feedlock
            var blobs = new List<IListBlobItem>();

            do
            {
                var result = await _container.ListBlobsSegmentedAsync(prefix, useFlatBlobListing, blobListingDetails, maxResults, continuationToken, options: null, operationContext: null);
                blobs.AddRange(result.Results);
            }
            while (continuationToken != null);

            // Skip the feed lock, and limit this to the current sub feed.
            return blobs.Where(e => !e.Uri.AbsoluteUri.EndsWith($"/{AzureFileSystemLock.LockFile}"))
                 .Where(e => string.IsNullOrEmpty(FeedSubPath) || e.Uri.AbsoluteUri.StartsWith(UriUtility.EnsureTrailingSlash(BaseURI).AbsoluteUri, StringComparison.Ordinal))
                 .Select(e => Get(e.Uri))
                 .ToList();
        }

        /// <summary>
        /// Get the path without the container root URI.
        /// </summary>
        private string GetPathRelativeToContainer(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            var path = uri.AbsoluteUri;

            if (!path.StartsWith(_containerRoot, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unable to make '{uri.AbsoluteUri}' relative to '{_containerRoot}'");
            }

            return path.Replace(_containerRoot, string.Empty);
        }
    }
}