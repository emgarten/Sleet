using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
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
        private readonly string _acl;

        private bool? _hasBucket;

        public AmazonS3FileSystem(LocalCache cache, Uri root, IAmazonS3 client, string bucketName, string acl)
            : this(cache, root, root, client, bucketName, ServerSideEncryptionMethod.None, acl: acl)
        {
        }

        public AmazonS3FileSystem(LocalCache cache,
            Uri root,
            Uri baseUri,
            IAmazonS3 client,
            string bucketName,
            ServerSideEncryptionMethod serverSideEncryptionMethod,
            string feedSubPath = null,
            bool compress = true,
            string acl = null)
            : base(cache, root, baseUri)
        {
            _client = client;
            _bucketName = bucketName;
            _serverSideEncryptionMethod = serverSideEncryptionMethod;
            _acl = acl;

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
            var sleetFiles = new List<ISleetFile>();
            var files = await GetFilesAsync(_client, _bucketName, token);

            foreach (var file in files)
            {
                var hasSubPath = !string.IsNullOrEmpty(FeedSubPath) && file.Key.StartsWith(FeedSubPath, StringComparison.Ordinal);

                // Filter to the feed sub path if one exists
                if (string.IsNullOrEmpty(FeedSubPath) || hasSubPath)
                {
                    var relPath = file.Key;

                    // Remove sub path if it exists on the relative path, GetPath will add the sub path.
                    if (hasSubPath)
                    {
                        relPath = relPath.Substring(FeedSubPath.Length);
                    }

                    // Get the URI including the bucket uri
                    var fileUri = GetPath(relPath);

                    // Skip the lock file
                    if (!fileUri.AbsoluteUri.EndsWith($"/{AmazonS3FileSystemLock.LockFile}", StringComparison.Ordinal))
                    {
                        // Get the ISleetFile
                        var sleetFile = Get(fileUri);
                        sleetFiles.Add(sleetFile);
                    }
                }
            }

            return sleetFiles;
        }

        private ISleetFile CreateAmazonS3File(SleetUriPair pair)
        {
            var key = GetRelativePath(pair.Root);
            return new AmazonS3File(this, pair.Root, pair.BaseURI, LocalCache.GetNewTempPath(), _client, _bucketName, key, _serverSideEncryptionMethod, _compress, _acl);
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
                    log.LogWarning($"Transient errors may happen during creation. Bucket already created: {ex.Message}.");
                }

                log.LogInformation($"Adding policy for public read access to bucket: ${_bucketName}");

                // As of 2023-04 additional settings are needed to allow a public policy
                // https://stackoverflow.com/questions/39085360/why-is-uploading-a-file-to-s3-via-the-c-sharp-aws-sdk-giving-a-permission-denied

                // Account wide settings can also block public access, users must update their accounts manually for this
                // https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/aws-properties-s3-bucket-publicaccessblockconfiguration.html

                // Remove all public access blocks for this bucket
                await Retry(SetPublicAccessBlocks, log, token);

                // Set ownership preference to ensure we can set a public policy
                await Retry(SetOwnership, log, token);

                // Set the public policy to public read-only
                await Retry(SetBucketPolicy, log, token);

                // Set the default acl of the bucket. Must not conflict with the public access policy.
                if (_acl != null)
                {
                    await Retry(SetBucketAcl, log, token);
                }

                // Get and release the lock to ensure that everything will work for the next operation.
                // In the E2E tests there are often failures due to the bucket saying it is not available
                // even though the above checks passed. To work around this wait until a file can be
                // successfully created in the bucket before returning.
                await CreateAndReleaseLock(log, token);

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

        // Set ObjectOwnership to BucketOwnerPreferred to allow for setting the public access policy.
        private Task SetOwnership(ILogger log, CancellationToken token)
        {
            var ownerReq = new PutBucketOwnershipControlsRequest()
            {
                BucketName = _bucketName,
                OwnershipControls = new OwnershipControls()
                {
                    Rules = new List<OwnershipControlsRule>
                            {
                                new() {
                                    ObjectOwnership = ObjectOwnership.BucketOwnerPreferred
                                }
                            }
                }
            };

            return _client.PutBucketOwnershipControlsAsync(ownerReq, token);
        }

        // Set the default acl of the bucket.
        private Task SetBucketAcl(ILogger log, CancellationToken token)
        {
            var aclReq = new PutACLRequest()
            {
                BucketName = _bucketName,
                CannedACL = S3CannedACL.FindValue(_acl)
            };

            return _client.PutACLAsync(aclReq, token);
        }

        // Remove public access blocks to allow public policies.
        private Task SetPublicAccessBlocks(ILogger log, CancellationToken token)
        {
            var blockReq = new PutPublicAccessBlockRequest()
            {
                BucketName = _bucketName,
                PublicAccessBlockConfiguration = new PublicAccessBlockConfiguration()
                {
                    IgnorePublicAcls = false,
                    RestrictPublicBuckets = false,
                    BlockPublicAcls = false,
                    BlockPublicPolicy = false,
                }
            };

            return _client.PutPublicAccessBlockAsync(blockReq, token);
        }

        // Set bucket policy to public ready-only
        private Task SetBucketPolicy(ILogger log, CancellationToken token)
        {
            var policyRequest = new PutBucketPolicyRequest()
            {
                BucketName = _bucketName,
                Policy = GetReadOnlyPolicy(_bucketName).ToString(),
            };


            return _client.PutBucketPolicyAsync(policyRequest, token);
        }

        // Retry S3 exceptions except for auth errors and bad requests
        private static async Task Retry(Func<ILogger, CancellationToken, Task> func, ILogger log, CancellationToken token)
        {
            var start = DateTime.UtcNow;
            var maxTime = TimeSpan.FromMinutes(2);
            var failures = 0;

            while (true)
            {
                try
                {
                    await func(log, token);

                    // end
                    return;
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
                {
                    // Forbidden indicates that the account info is correct, but the account lacks permissions, or the public access policy is blocking the change.
                    // The user may not need AmazonS3FullAccess if they set a specific policy, but for diagnostics purposes it is the easiest way to help them rule out S3 access problems.
                    log.LogWarning("Unable to update S3 bucket. Ensure that the login info used has AmazonS3FullAccess and that the AWS account allows public access buckets: https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/aws-properties-s3-bucket-publicaccessblockconfiguration.html");
                    log.LogWarning($"Failed to update S3 bucket. Status code: {ex.StatusCode} error: {ex.Message}");
                    throw;
                }
                catch (AmazonS3Exception ex) when (DateTime.UtcNow < start.Add(maxTime) && CanRetry(ex))
                {
                    // Only show the warning once.
                    // The last exception will throw.
                    if (failures == 0)
                    {
                        log.LogWarning($"Failed to update S3 bucket. Trying again. Status code: {ex.StatusCode} error: {ex.Message}");
                    }

                    failures++;

                    // Ignore exceptions until the last exception
                    await Task.Delay(TimeSpan.FromMilliseconds(500), token);
                }
            }
        }

        // True if the exception should be retried
        private static bool CanRetry(AmazonS3Exception ex)
        {
            switch (ex.StatusCode)
            {
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.Forbidden:
                case HttpStatusCode.Unauthorized:
                    return false;
                default:
                    return true;
            }
        }

        // Create and release a lock to ensure it will work after the bucket is created.
        private async Task CreateAndReleaseLock(ILogger log, CancellationToken token)
        {
            var start = DateTime.UtcNow;
            var maxTime = TimeSpan.FromMinutes(2);

            // Pass the null logger to avoid noise
            using (var feedLock = CreateLock(NullLogger.Instance))
            {
                while (true)
                {
                    try
                    {
                        // Attempt to get the lock, since this is a new container it will be available.
                        // This will fail if the bucket it not yet ready.
                        if (await feedLock.GetLock(TimeSpan.FromMinutes(1), "Container create lock test", token))
                        {
                            feedLock.Release();
                            return;
                        }

                        if (DateTime.UtcNow > start.Add(maxTime))
                        {
                            throw new InvalidOperationException("Unable to initialize lock for new bucket.");
                        }
                    }
                    catch (AmazonS3Exception ex) when (DateTime.UtcNow < start.Add(maxTime) && CanRetry(ex))
                    {
                        // Ignore exceptions until the last exception
                        await Task.Delay(TimeSpan.FromMilliseconds(500), token);
                    }
                }
            }
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
