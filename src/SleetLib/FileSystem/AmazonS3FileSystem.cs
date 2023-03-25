using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using static Sleet.AmazonS3FileSystemAbstraction;

namespace Sleet
{
    public class AmazonS3FileSystem : FileSystemBase
    {
        private readonly string _bucketName;
        private readonly IAmazonS3 _client;
        private readonly bool _compress;
        private readonly ServerSideEncryptionMethod _serverSideEncryptionMethod;

        private bool? _hasBucket;

        public AmazonS3FileSystem(LocalCache cache, Uri root, IAmazonS3 client, string bucketName)
            : this(cache, root, root, client, bucketName, ServerSideEncryptionMethod.None)
        {
        }

        public AmazonS3FileSystem(
            LocalCache cache,
            Uri root,
            Uri baseUri,
            IAmazonS3 client,
            string bucketName,
            ServerSideEncryptionMethod serverSideEncryptionMethod,
            string feedSubPath = null,
            bool compress = true)
            : base(cache, root, baseUri)
        {
            _client = client;
            _bucketName = bucketName;
            _serverSideEncryptionMethod = serverSideEncryptionMethod;

            if (!string.IsNullOrEmpty(feedSubPath))
            {
                FeedSubPath = feedSubPath.Trim('/') + '/';
            }

            _compress = compress;
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
            return new AmazonS3FileSystemLock(_client, _bucketName, _serverSideEncryptionMethod, log);
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
            return new AmazonS3File(this, pair.Root, pair.BaseURI, LocalCache.GetNewTempPath(), _client, _bucketName, key, _serverSideEncryptionMethod, _compress);
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
                _hasBucket = await AmazonS3Util.DoesS3BucketExistV2Async(_client, _bucketName);
            }

            return _hasBucket == true;
        }

        public override async Task CreateBucket(ILogger log, CancellationToken token)
        {

            if (!await HasBucket(log, token))
            {
                log.LogInformation($"Creating new bucket: {_bucketName}");
                try
                {
                    await _client.EnsureBucketExistsAsync(_bucketName);
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    // Ignore the Conflict exception when creating the bucket
                    log.LogWarning($"Transient error may happen during creation. Bucket already created: {ex.Message}.");
                }

                var tries = 0;
                var maxTries = 30;
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

                // Get and release the lock to ensure that everything will work for the next operation.
                // In the E2E tests there are often failures due to the bucket saying it is not available
                // even though the above checks passed. To work around this wait until a file can be
                // successfully created in the bucket before returning.
                tries = 0;
                success = false;

                // Pass the null logger to avoid noise
                using (var feedLock = CreateLock(NullLogger.Instance))
                {
                    while (tries < maxTries && !success)
                    {
                        tries++;

                        try
                        {
                            // Attempt to get the lock, since this is a new container it will be available.
                            // This will fail if the bucket it not yet ready.
                            if (await feedLock.GetLock(TimeSpan.FromMinutes(1), "Container create lock test", token))
                            {
                                feedLock.Release();
                                success = true;
                            }
                        }
                        catch (AmazonS3Exception ex) when (tries < (maxTries - 1) && ex.StatusCode != HttpStatusCode.BadRequest)
                        {
                            // Ignore exceptions until the last exception
                            await Task.Delay(TimeSpan.FromSeconds(tries));
                        }
                    }
                }

                _hasBucket = true;
            }
        }

        public override async Task DeleteBucket(ILogger log, CancellationToken token)
        {
            if (await HasBucket(log, token))
            {
                try
                {
                    await _client.DeleteBucketAsync(_bucketName, token);
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // Ignore the NotFound exception when deleting the bucket
                    log.LogWarning($"Transient error may happen during deletion. S3 bucket does not exist any more: {ex.Message}.");
                }
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
