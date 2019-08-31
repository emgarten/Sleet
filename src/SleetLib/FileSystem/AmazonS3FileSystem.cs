#if !SLEETLEGACY
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using static Sleet.AmazonS3FileSystemAbstraction;

namespace Sleet
{
    public class AmazonS3FileSystem : FileSystemBase
    {
        private readonly string _bucketName;
        private readonly IAmazonS3 _client;
        private bool? _hasBucket;

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
            : base(cache, root, baseUri)
        {
            _client = client;
            _bucketName = bucketName;

            if (!string.IsNullOrEmpty(feedSubPath))
            {
                FeedSubPath = feedSubPath.Trim('/') + '/';
            }
        }

        public override async Task<bool> Validate(ILogger log, CancellationToken token)
        {
            log.LogInformation($"Verifying {_bucketName} exists.");

            var isBucketFound = await HasBucket(log, token);
            if (!isBucketFound)
            {
                log.LogError(
                    $"Unable to find {_bucketName}. Verify that the Amazon account and bucket exists. The bucket " +
                    "must be created manually before using this feed.");
            }

            return isBucketFound;
        }

        public override ISleetFileSystemLock CreateLock(ILogger log)
        {
            return new AmazonS3FileSystemLock(_client, _bucketName, log);
        }

        public override ISleetFile Get(Uri path)
        {
            return GetOrAddFile(path,
                caseSensitive: true,
                createFile: (pair) => CreateAmazonS3File(pair));
        }

        public override async Task<IReadOnlyList<ISleetFile>> GetFiles(ILogger log, CancellationToken token)
        {
            return (await GetFilesAsync(_client, _bucketName, token))
                .Where(x => !x.Key.Equals(AmazonS3FileSystemLock.LockFile))
                .Select(x => Get(GetPath(x.Key)))
                .ToList();
        }

        private ISleetFile CreateAmazonS3File(SleetUriPair pair)
        {
            var key = GetRelativePath(pair.Root);
            return new AmazonS3File(this, pair.Root, pair.BaseURI, LocalCache.GetNewTempPath(), _client, _bucketName, key);
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
            if (_hasBucket == null)
            {
                _hasBucket = await _client.DoesS3BucketExistAsync(_bucketName);
            }

            return _hasBucket == true;
        }

        public override async Task CreateBucket(ILogger log, CancellationToken token)
        {

            if (!await HasBucket(log, token))
            {
                log.LogInformation($"Creating new bucket: ${_bucketName}");
                await _client.EnsureBucketExistsAsync(_bucketName);

                var tries = 0;
                var maxTries = 10;
                var success = false;

                while (tries < maxTries && !success)
                {
                    tries++;

                    try
                    {
                        var policyRequest = new PutBucketPolicyRequest()
                        {
                            BucketName = _bucketName,
                            Policy = GetReadOnlyPolicy(_bucketName).ToString()
                        };

                        log.LogInformation($"Adding policy for public read access to bucket: ${_bucketName}");
                        await _client.PutBucketPolicyAsync(policyRequest);
                        success = true;
                    }
                    catch (AmazonS3Exception ex) when (tries < (maxTries - 1))
                    {
                        log.LogWarning($"Failed to update policy for bucket: {ex.Message} Trying again.");
                        await Task.Delay(TimeSpan.FromSeconds(tries));
                    }
                }

                if (tries >= maxTries)
                {
                    throw new InvalidOperationException("Unable to create bucket");
                }

                _hasBucket = true;
            }
        }

        public override async Task DeleteBucket(ILogger log, CancellationToken token)
        {
            if (await HasBucket(log, token))
            {
                await _client.DeleteBucketAsync(_bucketName, token);
            }
            _hasBucket = false;
        }

        private static JObject GetReadOnlyPolicy(string bucketName)
        {
            // Grant read-only access based on https://aws.amazon.com/premiumsupport/knowledge-center/read-access-objects-s3-bucket/
            return new JObject(
                new JProperty("Version", "2012-10-17"),
                new JProperty("Statement",
                    new JArray(
                        new JObject(
                            new JProperty("Sid", "AllowPublicRead"),
                            new JProperty("Effect", "Allow"),
                            new JProperty("Principal", "*"),
                            new JProperty("Action", new JArray("s3:GetObject")),
                            new JProperty("Resource", new JArray($"arn:aws:s3:::{bucketName}/*"))
                        ))));
        }
    }
}
#endif