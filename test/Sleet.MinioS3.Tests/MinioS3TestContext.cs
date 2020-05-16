using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using NuGet.Common;
using NuGet.Test.Helpers;

namespace Sleet.MinioS3.Tests
{
    public class MinioS3TestContext : IDisposable
    {
        public const string EnvAccessKeyId = "AWS_ACCESS_KEY_ID";
        public const string EnvSecretAccessKey = "AWS_SECRET_ACCESS_KEY";
        public const string EnvDefaultRegion = "AWS_DEFAULT_REGION";

        public const string EnvServiceURL = "SLEET_FEED_SERVICEURL";

        private bool cleanupDone = false;

        public MinioS3TestContext()
        {
            BucketName = $"sleet-test-{Guid.NewGuid().ToString()}";
            LocalCache = new LocalCache();
            LocalSettings = new LocalSettings();

            var accessKeyId = Environment.GetEnvironmentVariable(EnvAccessKeyId);
            var secretAccessKey = Environment.GetEnvironmentVariable(EnvSecretAccessKey);
            var region = Environment.GetEnvironmentVariable(EnvDefaultRegion) ?? "us-east-1";
            var serviceURL = Environment.GetEnvironmentVariable(EnvServiceURL) ?? "http://localhost:9000";
            
            var config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(region),
                ServiceURL = serviceURL,
                ForcePathStyle = true
            };

            Client = new AmazonS3Client(accessKeyId, secretAccessKey, config);
            Uri = MinioS3Utility.GetBucketPath(BucketName, serviceURL);

            FileSystem = new AmazonS3FileSystem(LocalCache, Uri, Client, BucketName);
            Logger = new TestLogger();
        }

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

            if (await Client.DoesS3BucketExistAsync(BucketName))
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

        public void Dispose()
        {
            LocalCache.Dispose();

            if (!cleanupDone)
            {
                CleanupAsync().Wait();
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