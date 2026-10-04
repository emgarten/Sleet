using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
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

        /// <summary>
        /// The host port of RustFS in local-env.
        /// </summary>
        public const int LocalEnvPort = 9100;

        /// <summary>
        /// The access key and secret key of RustFS in local-env.
        /// </summary>
        public const string LocalEnvCredential = "rustfsadmin";

        private bool cleanupDone = false;

        public AmazonS3TestContext(string acl = null)
        {
            BucketName = $"sleet-test-{Guid.NewGuid().ToString()}";
            LocalCache = new LocalCache();
            LocalSettings = new LocalSettings();

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
            }

            Client = new AmazonS3Client(AccessKeyId, SecretAccessKey, config);
            FileSystem = new AmazonS3FileSystem(LocalCache, Uri, Client, BucketName, acl);
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

        public static string Region
        {
            get
            {
                var region = HasAccount ? Environment.GetEnvironmentVariable(EnvRegion) : null;
                return string.IsNullOrEmpty(region) ? "us-east-1" : region;
            }
        }

        /// <summary>
        /// True if the access key and secret are set or RustFS in local-env is running.
        /// </summary>
        public static bool IsAvailable => HasAccount || LocalEnvironment.IsListening(LocalEnvPort);

        public string BucketName { get; }

        public IAmazonS3 Client { get; }

        public LocalSettings LocalSettings { get; }

        public AmazonS3FileSystem FileSystem { get; set; }

        public LocalCache LocalCache { get; }

        public Uri Uri { get; set; }

        public ILogger Logger { get; }

        public bool CreateBucketOnInit = true;

        /// <summary>
        /// The s3 source for sleet.json that uses the test bucket.
        /// </summary>
        public JObject GetSourceSettings(string name)
        {
            var source = new JObject(
                new JProperty("name", name),
                new JProperty("type", "s3"),
                new JProperty("bucketName", BucketName),
                new JProperty("region", Region),
                new JProperty("accessKeyId", AccessKeyId),
                new JProperty("secretAccessKey", SecretAccessKey));

            if (!string.IsNullOrEmpty(ServiceUrl))
            {
                source.Add("serviceURL", ServiceUrl);
                source.Add("forcePathStyle", true);
            }

            return source;
        }

        public async Task CleanupAsync()
        {
            cleanupDone = true;

            if (await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(Client, BucketName))
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
            if (CreateBucketOnInit)
            {
                await Client.EnsureBucketExistsAsync(BucketName);
            }
        }
    }
}