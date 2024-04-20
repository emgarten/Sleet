using Azure.Storage.Blobs;
using NuGet.Common;
using Azure.Storage.Blobs.Models;

namespace Sleet
{
    public class AzureFileSystem : FileSystemBase
    {
        public static readonly string AzureEmptyConnectionString = "DefaultEndpointsProtocol=https;AccountName=;AccountKey=;BlobEndpoint=";

        private readonly BlobContainerClient _container;

        public AzureFileSystem(LocalCache cache, Uri root, BlobServiceClient blobServiceClient, string container)
            : this(cache, root, root, blobServiceClient, container)
        {
        }

        public AzureFileSystem(LocalCache cache, Uri root, Uri baseUri, BlobServiceClient blobServiceClient, string container, string feedSubPath = null)
            : base(cache, root, baseUri, feedSubPath)
        {
            _container = blobServiceClient.GetBlobContainerClient(container);

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
                    _container.GetBlobClient(GetRelativePath(path))));
        }

        public override async Task<bool> Validate(ILogger log, CancellationToken token)
        {
            log.LogInformation($"Verifying {_container.Uri.AbsoluteUri} exists.");

            if (await _container.ExistsAsync(token))
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
            var blob = _container.GetBlobClient(GetRelativePath(GetPath(AzureFileSystemLock.LockFile)));
            var messageBlob = _container.GetBlobClient(GetRelativePath(GetPath(AzureFileSystemLock.LockFileMessage)));
            return new AzureFileSystemLock(blob, messageBlob, log);
        }

        public override async Task<IReadOnlyList<ISleetFile>> GetFiles(ILogger log, CancellationToken token)
        {
            var results = _container.GetBlobsAsync();
            var pages = results.AsPages();
            var blobs = new List<ISleetFile>();

            await foreach (var page in pages)
            {
                // process page
                blobs.AddRange(
                    page.Values
                        .Where(item => !item.Name.EndsWith(AzureFileSystemLock.LockFile, StringComparison.Ordinal))
                        .Where(item =>
                            string.IsNullOrEmpty(FeedSubPath) ||
                            item.Name.StartsWith(FeedSubPath, StringComparison.Ordinal))
                        .Select(item =>
                            Get(new BlobUriBuilder(_container.Uri) { BlobName = item.Name }.ToUri())
                            ));
            }

            return blobs;
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

        public override async Task<bool> HasBucket(ILogger log, CancellationToken token)
        {
            return await _container.ExistsAsync(token);
        }

        public override async Task CreateBucket(ILogger log, CancellationToken token)
        {
            await _container.CreateIfNotExistsAsync(PublicAccessType.BlobContainer, null, null, token);
        }

        public override async Task DeleteBucket(ILogger log, CancellationToken token)
        {
            await _container.DeleteIfExistsAsync(cancellationToken: token);
        }
    }
}
