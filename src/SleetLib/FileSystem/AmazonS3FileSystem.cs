using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using NuGet.Common;
using static Sleet.AmazonS3FileSystemAbstraction;

namespace Sleet
{
    public class AmazonS3FileSystem : FileSystemBase
    {
        private readonly string bucketName;
        private readonly IAmazonS3 client;

        public AmazonS3FileSystem(LocalCache cache, Uri root, IAmazonS3 client, string bucketName)
            : this(cache, root, root, client, bucketName)
        {
        }

        public AmazonS3FileSystem(
            LocalCache cache,
            Uri root,
            Uri baseUri,
            IAmazonS3 client,
            string bucketName,
            string feedSubPath = null)
            : base(cache, root, baseUri, feedSubPath)
        {
            this.client = client;
            this.bucketName = bucketName;
        }

        public override async Task<bool> Validate(ILogger log, CancellationToken token)
        {
            log.LogInformation($"Verifying {bucketName} exists.");

            bool isBucketFound = await client.DoesS3BucketExistAsync(bucketName).ConfigureAwait(false);
            if (!isBucketFound)
            {
                log.LogError(
                    $"Unable to find {bucketName}. Verify that the Amazon account and bucket exists. The bucket " +
                    "must be created manually before using this feed.");
            }

            return isBucketFound;
        }

        public override ISleetFileSystemLock CreateLock(ILogger log)
        {
            return new AmazonS3FileSystemLock(client, bucketName, log);
        }

        public override ISleetFile Get(Uri path)
        {
            return Files.GetOrAdd(path, CreateAmazonS3File);
        }

        public override async Task<IReadOnlyList<ISleetFile>> GetFiles(ILogger log, CancellationToken token)
        {
            return (await GetFilesAsync(client, bucketName, token))
                .Where(x => !x.Key.Equals(AmazonS3FileSystemLock.LockFile))
                .Select(x => Get(GetPath(x.Key)))
                .ToList();
        }

        private ISleetFile CreateAmazonS3File(Uri uri)
        {
            Uri rootUri = UriUtility.ChangeRoot(BaseURI, Root, uri);
            string key = GetPathRelativeToBucket(uri);
            return new AmazonS3File(this, rootUri, uri, LocalCache.GetNewTempPath(), client, bucketName, key);
        }

        private string GetPathRelativeToBucket(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            string baseUri = BaseURI.ToString();
            string path = uri.AbsoluteUri;

            if (!path.StartsWith(baseUri, StringComparison.Ordinal))
                throw new InvalidOperationException($"Unable to make '{uri.AbsoluteUri}' relative to '{baseUri}'");

            string key = path.Replace(baseUri, string.Empty);
            if (!string.IsNullOrEmpty(FeedSubPath))
            {
                key = FeedSubPath[FeedSubPath.Length - 1] == '/'
                    ? FeedSubPath + key
                    : FeedSubPath + "/" + key;
            }

            return key;
        }
    }
}