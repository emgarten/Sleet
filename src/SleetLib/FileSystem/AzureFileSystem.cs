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

            var containerUri = UriUtility.EnsureTrailingSlash(_container.Uri);
            var expectedPath = UriUtility.EnsureTrailingSlash(root);

            // Verify that the provided path is sane.
            if (!expectedPath.AbsoluteUri.StartsWith(expectedPath.AbsoluteUri, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Invalid feed path. Azure container {container} resolved to {containerUri.AbsoluteUri} which does not match the provided URI of {expectedPath}  Update path in sleet.json or remove the path property to auto resolve the value.");
            }

            // Compute sub path, ignore the given sub path
            var subPath = UriUtility.GetRelativePath(
                containerUri,
                expectedPath);

            if (!string.IsNullOrEmpty(subPath))
            {
                // Override the given sub path
                FeedSubPath = subPath;
            }

            if (!string.IsNullOrEmpty(FeedSubPath))
            {
                FeedSubPath = FeedSubPath.Trim('/') + '/';
            }
        }

        public override ISleetFile Get(Uri path)
        {
            return GetOrAddFile(path, caseSensitive: true,
                createFile: (pair) => new AzureFile(
                    this,
                    pair.Root,
                    pair.BaseURI,
                    LocalCache.GetNewTempPath(),
                    _container.GetBlockBlobReference(GetRelativePath(path))));
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
            // Create blobs
            var blob = _container.GetBlockBlobReference(GetRelativePath(GetPath(AzureFileSystemLock.LockFile)));
            var messageBlob = _container.GetBlockBlobReference(GetRelativePath(GetPath(AzureFileSystemLock.LockFileMessage)));
            return new AzureFileSystemLock(blob, messageBlob, log);
        }

        public override async Task<IReadOnlyList<ISleetFile>> GetFiles(ILogger log, CancellationToken token)
        {
            string prefix = null;
            var useFlatBlobListing = true;
            var blobListingDetails = BlobListingDetails.All;
            int? maxResults = null;

            // Return all files except feedlock
            var blobs = new List<IListBlobItem>();

            BlobResultSegment result = null;
            do
            {
                result = await _container.ListBlobsSegmentedAsync(prefix, useFlatBlobListing, blobListingDetails, maxResults, result?.ContinuationToken, options: null, operationContext: null);
                blobs.AddRange(result.Results);
            }
            while (result.ContinuationToken != null);

            // Skip the feed lock, and limit this to the current sub feed.
            return blobs.Where(e => !e.Uri.AbsoluteUri.EndsWith($"/{AzureFileSystemLock.LockFile}"))
                 .Where(e => string.IsNullOrEmpty(FeedSubPath) || e.Uri.AbsoluteUri.StartsWith(UriUtility.EnsureTrailingSlash(BaseURI).AbsoluteUri, StringComparison.Ordinal))
                 .Select(e => Get(e.Uri))
                 .ToList();
        }

        public override string GetRelativePath(Uri uri)
        {
            var relativePath = base.GetRelativePath(uri);

            if (!string.IsNullOrEmpty(FeedSubPath))
            {
                relativePath = FeedSubPath + relativePath;
            }

            return relativePath;
        }
    }
}
