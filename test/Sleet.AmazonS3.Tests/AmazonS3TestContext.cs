using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using NuGet.Common;
using NuGet.Test.Helpers;

namespace Sleet.AmazonS3.Tests
{
    public class AmazonS3TestContext : IAsyncDisposable
    {
        // Sleet specific names, so that AWS credentials in the environment don't run the tests against a real account
        public const string EnvAccessKeyId = "SLEET_TEST_S3_ACCESS_KEY_ID";
        public const string EnvSecretAccessKey = "SLEET_TEST_S3_SECRET_ACCESS_KEY";
        public const string EnvRegion = "SLEET_TEST_S3_REGION";

        private bool cleanupDone = false;

        public AmazonS3TestContext(string acl = null)
        {
            BucketName = $"sleet-test-{Guid.NewGuid().ToString()}";
            LocalCache = new LocalCache();
            LocalSettings = new LocalSettings();

            var region = Region;
            var config = new AmazonS3Config()
            {
                Timeout = TimeSpan.FromSeconds(100),
                RegionEndpoint = RegionEndpoint.GetBySystemName(region)
            };
            Client = new AmazonS3Client(AccessKeyId, SecretAccessKey, config);
            Uri = AmazonS3Utility.GetBucketPath(BucketName, region);

            FileSystem = new AmazonS3FileSystem(LocalCache, Uri, Client, BucketName, acl);
            Logger = new TestLogger();
        }

        public static string AccessKeyId => Environment.GetEnvironmentVariable(EnvAccessKeyId);

        public static string SecretAccessKey => Environment.GetEnvironmentVariable(EnvSecretAccessKey);

        public static string Region
        {
            get
            {
                var region = Environment.GetEnvironmentVariable(EnvRegion);
                return string.IsNullOrEmpty(region) ? "us-east-1" : region;
            }
        }

        /// <summary>
        /// True if the access key and secret are set.
        /// </summary>
        public static bool IsAvailable => !string.IsNullOrEmpty(AccessKeyId) && !string.IsNullOrEmpty(SecretAccessKey);

        public string BucketName { get; }

        public IAmazonS3 Client { get; }

        public LocalSettings LocalSettings { get; }

        public AmazonS3FileSystem FileSystem { get; set; }

        public LocalCache LocalCache { get; }

        public Uri Uri { get; set; }

        public ILogger Logger { get; }

        public bool CreateBucketOnInit = true;

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