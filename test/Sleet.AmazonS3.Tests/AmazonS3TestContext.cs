using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Test.Helpers;
using Sleet.Test.Common;

namespace Sleet.AmazonS3.Tests
{
    public class AmazonS3TestContext : IAsyncDisposable
    {
        // Sleet specific names, so that AWS credentials in the environment don't run the tests against a real account
        public const string EnvAccessKeyId = "SLEET_TEST_S3_ACCESS_KEY_ID";
        public const string EnvSecretAccessKey = "SLEET_TEST_S3_SECRET_ACCESS_KEY";
        public const string EnvRegion = "SLEET_TEST_S3_REGION";
        public const string EnvServiceUrl = "SLEET_TEST_S3_SERVICE_URL";
        public const string EnvProvider = "SLEET_TEST_S3_PROVIDER";

        /// <summary>
        /// The host port of RustFS in local-env.
        /// </summary>
        public const int LocalEnvPort = 9100;

        /// <summary>
        /// The access key and secret key of RustFS in local-env.
        /// </summary>
        public const string LocalEnvCredential = "rustfsadmin";

        private readonly string acl;
        private bool cleanupDone = false;

        public AmazonS3TestContext(string acl = null)
        {
            this.acl = acl;
            BucketName = $"sleet-test-{Guid.NewGuid().ToString()}";
            LocalCache = new LocalCache();
            LocalSettings = new LocalSettings();

            // Client used by the tests to create, inspect and delete the bucket
            var config = new AmazonS3Config()
            {
                Timeout = TimeSpan.FromSeconds(100)
            };

            if (string.IsNullOrEmpty(ServiceUrl))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(Region);
                Uri = AmazonS3Utility.GetBucketPath(BucketName, Region);
            }
            else
            {
                // S3 compatible storage, with path style urls since the bucket name may not resolve as a host name
                config.ServiceURL = ServiceUrl;
                config.AuthenticationRegion = Region;
                config.ForcePathStyle = true;
                Uri = new Uri($"{ServiceUrl.TrimEnd('/')}/{BucketName}/");

                if (IsCloudflareR2)
                {
                    // The same checksums as the r2 provider
                    config.RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED;
                    config.ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED;
                }
            }

            Client = new AmazonS3Client(AccessKeyId, SecretAccessKey, config);
            Logger = new TestLogger();
        }

        /// <summary>
        /// True if the access key and secret are set. Otherwise the tests use RustFS in local-env.
        /// </summary>
        public static bool HasAccount => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvAccessKeyId))
            && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvSecretAccessKey));

        public static string AccessKeyId => HasAccount ? Environment.GetEnvironmentVariable(EnvAccessKeyId) : LocalEnvCredential;

        public static string SecretAccessKey => HasAccount ? Environment.GetEnvironmentVariable(EnvSecretAccessKey) : LocalEnvCredential;

        /// <summary>
        /// The S3 compatible storage to test against, or null for Amazon S3.
        /// </summary>
        public static string ServiceUrl => HasAccount ? Environment.GetEnvironmentVariable(EnvServiceUrl) : $"http://127.0.0.1:{LocalEnvPort}";

        /// <summary>
        /// The region to sign requests for.
        /// </summary>
        public static string Region => string.IsNullOrEmpty(RegionSetting) ? "us-east-1" : RegionSetting;

        private static string RegionSetting => HasAccount ? Environment.GetEnvironmentVariable(EnvRegion) : null;

        /// <summary>
        /// The provider setting of the source, self-hosted by default for S3 compatible storage. Null for Amazon S3.
        /// </summary>
        public static string Provider
        {
            get
            {
                var provider = HasAccount ? Environment.GetEnvironmentVariable(EnvProvider) : null;
                return string.IsNullOrEmpty(provider) && !string.IsNullOrEmpty(ServiceUrl) ? "self-hosted" : provider;
            }
        }

        public static bool IsCloudflareR2 => string.Equals(Provider, "r2", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// True if the access key and secret are set or RustFS in local-env is running.
        /// </summary>
        public static bool IsAvailable => HasAccount || LocalEnvironment.IsListening(LocalEnvPort);

        public string BucketName { get; }

        public IAmazonS3 Client { get; }

        public LocalSettings LocalSettings { get; }

        /// <summary>
        /// The file system under test. InitAsync creates it from GetSourceSettings if it isn't set.
        /// </summary>
        public AmazonS3FileSystem FileSystem { get; set; }

        public LocalCache LocalCache { get; }

        public Uri Uri { get; set; }

        public ILogger Logger { get; }

        public bool CreateBucketOnInit = true;

        /// <summary>
        /// The s3 source for sleet.json that uses the test bucket, with the settings from the docs.
        /// </summary>
        public JObject GetSourceSettings(string name)
        {
            var source = new JObject(
                new JProperty("name", name),
                new JProperty("type", "s3"),
                new JProperty("bucketName", BucketName),
                new JProperty("accessKeyId", AccessKeyId),
                new JProperty("secretAccessKey", SecretAccessKey));

            if (!string.IsNullOrEmpty(Provider))
            {
                source.Add("provider", Provider);
            }

            if (!string.IsNullOrEmpty(ServiceUrl))
            {
                source.Add("serviceURL", ServiceUrl);
            }

            // S3 compatible storage only needs a region if it doesn't use us-east-1
            if (string.IsNullOrEmpty(ServiceUrl) || !string.IsNullOrEmpty(RegionSetting))
            {
                source.Add("region", Region);
            }

            // R2 needs the public url of the bucket. The test buckets aren't public, so use the bucket url.
            if (IsCloudflareR2)
            {
                source.Add("baseURI", Uri.AbsoluteUri);
            }

            if (acl != null)
            {
                source.Add("acl", acl);
            }

            return source;
        }

        /// <summary>
        /// Create the file system for the test bucket from sleet.json settings, the same as the sleet commands do.
        /// </summary>
        public async Task<AmazonS3FileSystem> CreateFileSystemAsync(Action<JObject> configure = null)
        {
            var source = GetSourceSettings("s3");
            configure?.Invoke(source);

            var settings = LocalSettings.Load(new JObject(new JProperty("sources", new JArray(source))));

            return (AmazonS3FileSystem)await FileSystemFactory.CreateFileSystemAsync(settings, LocalCache, "s3", Logger);
        }

        /// <summary>
        /// Create the file system for a feed in a folder of the test bucket.
        /// </summary>
        public Task<AmazonS3FileSystem> CreateSubFeedFileSystemAsync(string feedSubPath)
        {
            return CreateFileSystemAsync(source =>
            {
                var path = UriUtility.GetPath(Uri, feedSubPath).AbsoluteUri;
                source.Add("path", path);
                source.Add("feedSubPath", feedSubPath);

                // baseURI must also end with the feed sub path
                if (source.ContainsKey("baseURI"))
                {
                    source["baseURI"] = path;
                }
            });
        }

        public async Task CleanupAsync()
        {
            cleanupDone = true;

            try
            {
                var s3Objects = (await AmazonS3FileSystemAbstraction
                    .GetFilesAsync(Client, BucketName, CancellationToken.None))
                    .Select(x => new KeyVersion { Key = x.Key })
                    .ToArray();

                if (s3Objects.Any())
                {
                    await AmazonS3FileSystemAbstraction.RemoveMultipleFilesAsync(
                        Client,
                        BucketName,
                        s3Objects,
                        CancellationToken.None);
                }

                await Client.DeleteBucketAsync(BucketName);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // The bucket wasn't created, or the test deleted it
            }
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            LocalCache.Dispose();

            if (!cleanupDone)
            {
                await CleanupAsync();
            }
        }

        public async Task InitAsync()
        {
            FileSystem ??= await CreateFileSystemAsync();

            if (CreateBucketOnInit)
            {
                await Client.EnsureBucketExistsAsync(BucketName);
            }
        }
    }
}