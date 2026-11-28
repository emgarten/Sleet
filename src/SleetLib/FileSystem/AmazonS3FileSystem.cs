using System.Net;
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
        private readonly bool _compress;
        private readonly ServerSideEncryptionMethod _serverSideEncryptionMethod;
        private readonly S3CannedACL? _acl;
        private readonly bool _disablePayloadSigning;
        private readonly string _immutableCacheControl;
        private readonly string _mutableCacheControl;

        private bool? _hasBucket;


        public AmazonS3FileSystem(LocalCache cache, Uri root, IAmazonS3 client, string bucketName, string acl, bool disablePayloadSigning = false)
            : this(cache, root, root, client, bucketName, ServerSideEncryptionMethod.None, acl: acl, disablePayloadSigning: disablePayloadSigning)
        {
        }

        public AmazonS3FileSystem(LocalCache cache,
            Uri root,
            Uri baseUri,
            IAmazonS3 client,
            string bucketName,
            ServerSideEncryptionMethod serverSideEncryptionMethod,
            string? feedSubPath = null,
            bool compress = true,
            S3CannedACL? acl = null,
            bool disablePayloadSigning = false,
            string? immutableCacheControl = null,
            string? mutableCacheControl = null)
            : base(cache, root, baseUri)
        {
            _client = client;
            _bucketName = bucketName;
            _serverSideEncryptionMethod = serverSideEncryptionMethod;
            _acl = acl;
            _disablePayloadSigning = disablePayloadSigning;
            _immutableCacheControl = immutableCacheControl ?? "no-store";
            _mutableCacheControl = mutableCacheControl ?? "no-store";

            if (!string.IsNullOrEmpty(feedSubPath))
            {
                FeedSubPath = feedSubPath.Trim('/') + '/';
            }

            _compress = compress;
        }

        /// <summary>
        /// Service the bucket is hosted on.
        /// </summary>
        internal S3Provider Provider { get; init; } = S3Provider.Aws;

        /// <summary>
        /// Delay before retrying a bucket create or delete that the service rate limited. Each retry waits a random time
        /// between this and 2.5 times this value.
        /// </summary>
        protected TimeSpan TooManyRequestsRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

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
            return new AmazonS3FileSystemLock(_client, _bucketName, _serverSideEncryptionMethod, _disablePayloadSigning, log);
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
                        relPath = relPath.Substring(FeedSubPath!.Length);
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

        private AmazonS3File CreateAmazonS3File(SleetUriPair pair)
        {
            var key = GetRelativePath(pair.Root);
            return new AmazonS3File(this, pair.Root, pair.BaseURI, LocalCache.GetNewTempPath(), _client, _bucketName, key, _serverSideEncryptionMethod, _compress, _acl, _disablePayloadSigning, _immutableCacheControl, _mutableCacheControl);
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
            _hasBucket ??= await BucketExists(token);

            return _hasBucket == true;
        }

        // HeadBucket is part of the core S3 API. GetBucketAcl, used by AmazonS3Util.DoesS3BucketExistV2Async, isn't implemented
        // by every S3 compatible service, and Cloudflare R2 documents it as unsupported.
        private async Task<bool> BucketExists(CancellationToken token)
        {
            try
            {
                await _client.HeadBucketAsync(new HeadBucketRequest() { BucketName = _bucketName }, token);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.MovedPermanently)
            {
                // Same as the SDK, the bucket exists but these credentials can't read it or it is in another region
                return true;
            }
        }

        public override async Task CreateBucket(ILogger log, CancellationToken token)
        {
            // A cached result can be old, the bucket may have been created since then
            if (_hasBucket != true)
            {
                _hasBucket = await BucketExists(token);
            }

            if (_hasBucket == false)
            {
                log.LogInformation($"Creating new bucket: {_bucketName}");

                // Only make a bucket public if this request created it. Something else may have created a private bucket
                // after the check above.
                if (await TryCreateBucket(log, token))
                {
                    await SetNewBucketAccess(log, token);
                }

                // Get and release the lock to ensure that everything will work for the next operation.
                // In the E2E tests there are often failures due to the bucket saying it is not available
                // even though the above checks passed. To work around this wait until a file can be
                // successfully created in the bucket before returning.
                await CreateAndReleaseLock(log, token);

                _hasBucket = true;
            }
        }

        // Returns true if this request created the bucket, or false if something else created it after the check for it
        private async Task<bool> TryCreateBucket(ILogger log, CancellationToken token)
        {
            try
            {
                // Amazon S3 needs the client region as the LocationConstraint. With serviceURL the SDK guesses the region from
                // the bucket url or the AWS environment, so a bucket name that contains ca-4247-9480 is sent as that region.
                // S3 compatible services choose a location when it is left out.
                await RetryTooManyRequests(() => _client.PutBucketAsync(new PutBucketRequest()
                {
                    BucketName = _bucketName,
                    UseClientRegion = Provider == S3Provider.Aws
                }, token), log, token);

                return true;
            }
            catch (BucketAlreadyExistsException ex) when (Provider == S3Provider.Aws)
            {
                // Amazon S3 only returns this when another account owns the bucket. Some S3 compatible services return it for
                // a bucket that the account owns.
                throw new InvalidOperationException($"Unable to create bucket {_bucketName}, the bucket name is already taken. Amazon S3 bucket names are shared by all AWS accounts, use a different bucketName. {ex.ErrorCode}: {ex.Message}", ex);
            }
            catch (AmazonS3Exception ex) when (ex is BucketAlreadyOwnedByYouException || ex.StatusCode == HttpStatusCode.Conflict)
            {
                // Something else created the bucket, or the SDK retried a request that created it. Amazon S3 in us-east-1 and
                // some S3 compatible services return success instead, which can't be told apart from a new bucket.
                log.LogWarning($"Bucket {_bucketName} was created after Sleet checked for it, so Sleet didn't change its access settings. For a public feed, allow public read access to the bucket. {ex.ErrorCode}: {ex.Message}");
                return false;
            }
        }

        // Make a new bucket publicly readable, for buckets created by Sleet
        private async Task SetNewBucketAccess(ILogger log, CancellationToken token)
        {
            if (Provider.ExternalPublicAccess)
            {
                log.LogWarning($"{Provider.DisplayName} buckets are private by default. Enable public access for {_bucketName} so that NuGet clients can read the feed: {Provider.HelpUrl}");
            }
            else if (await HasFiles(log, token))
            {
                // A bucket that Sleet created is empty. Amazon S3 can report a bucket that was just created as missing, and in
                // us-east-1 it returns success when the bucket already exists, the same as some S3 compatible services.
                log.LogWarning($"Bucket {_bucketName} already contains files, so Sleet didn't create it and didn't change its access settings. For a public feed, allow public read access to the bucket.");
            }
            else
            {
                log.LogInformation($"Adding policy for public read access to bucket: {_bucketName}");

                // As of 2023-04 additional settings are needed to allow a public policy
                // https://stackoverflow.com/questions/39085360/why-is-uploading-a-file-to-s3-via-the-c-sharp-aws-sdk-giving-a-permission-denied

                // Account wide settings can also block public access, users must update their accounts manually for this
                // https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/aws-properties-s3-bucket-publicaccessblockconfiguration.html

                // Remove all public access blocks for this bucket
                await RetryOptional(SetPublicAccessBlocks, log, token);

                // Set ownership preference to ensure we can set a public policy
                await RetryOptional(SetOwnership, log, token);

                // Set the public policy to public read-only
                await Retry(SetBucketPolicy, log, token);

                // Set the default acl of the bucket. Must not conflict with the public access policy.
                if (_acl != null)
                {
                    await Retry(SetBucketAcl, log, token);
                }
            }
        }

        // True if the bucket contains any files. Retry waits until a bucket that was just created is found.
        private async Task<bool> HasFiles(ILogger log, CancellationToken token)
        {
            var hasFiles = false;

            await Retry(async (_, retryToken) =>
            {
                var response = await _client.ListObjectsV2Async(new ListObjectsV2Request()
                {
                    BucketName = _bucketName,
                    MaxKeys = 1
                }, retryToken);

                hasFiles = response.S3Objects?.Count > 0;
            }, log, token);

            return hasFiles;
        }

        public override async Task DeleteBucket(ILogger log, CancellationToken token)
        {
            if (await HasBucket(log, token))
            {
                try
                {
                    // With serviceURL the SDK guesses the signing region from the bucket url or the AWS environment, so a bucket
                    // name that contains af-416c-83 is signed with that region and R2 rejects the request.
                    await RetryTooManyRequests(() => _client.DeleteBucketAsync(new DeleteBucketRequest()
                    {
                        BucketName = _bucketName,
                        UseClientRegion = Provider == S3Provider.Aws
                    }, token), log, token);
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
            var bucketAclReq = new PutBucketAclRequest()
            {
                BucketName = _bucketName,
                ACL = _acl
            };

            return _client.PutBucketAclAsync(bucketAclReq, token);
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
                    BlockPublicPolicy = false
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

        // Retry S3 exceptions except for auth errors, bad requests, and requests the service does not support
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

        // Retry a setting that Amazon S3 needs before it allows a public policy. S3 compatible services
        // may not support it, errors other than auth failures are skipped. If the setting was needed
        // the bucket policy will fail.
        private static async Task RetryOptional(Func<ILogger, CancellationToken, Task> func, ILogger log, CancellationToken token)
        {
            try
            {
                await Retry(func, log, token);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode != HttpStatusCode.Forbidden && ex.StatusCode != HttpStatusCode.Unauthorized)
            {
                log.LogVerbose($"Skipping bucket setting that is not supported by the service. Status code: {ex.StatusCode} error: {ex.Message}");
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
                case HttpStatusCode.MethodNotAllowed:
                case HttpStatusCode.NotImplemented:
                    return false;
                default:
                    return true;
            }
        }

        // Cloud Storage allows a project about one bucket create or delete every two seconds. The AWS SDK retries a 429 twice
        // within a few seconds, which isn't enough when several buckets are created or deleted at once. Wait a random time so
        // that the requests that were rejected together don't retry together.
        private async Task RetryTooManyRequests(Func<Task> request, ILogger log, CancellationToken token)
        {
            const int maxAttempts = 10;

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await request();
                    return;
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests && attempt < maxAttempts)
                {
                    // Only show the warning once
                    if (attempt == 1)
                    {
                        log.LogWarning($"S3 bucket request was rate limited. Trying again. Status code: {ex.StatusCode} error: {ex.Message}");
                    }

                    await Task.Delay(TooManyRequestsRetryDelay * (1 + Random.Shared.NextDouble() * 1.5), token);
                }
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
