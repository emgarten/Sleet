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
        public const string EnvAccessKeyId = "SLEET_FEED_ACCESSKEYID";
        public const string EnvSecretAccessKey = "SLEET_FEED_SECRETACCESSKEY";
        public const string EnvDefaultRegion = "SLEET_FEED_REGION";
        public const string EnvServiceURL = "SLEET_FEED_SERVICEURL";
        // public const string EnvCompress = "SLEET_FEED_COMPRESS";
        public const bool Compress = false;
        public const string EnvFeedType = "SLEET_FEED_TYPE";

        private bool cleanupDone = true;

        public MinioS3TestContext()
        {
            BucketName = $"sleet-test-{Guid.NewGuid().ToString()}";
            LocalCache = new LocalCache();
            LocalSettings = new LocalSettings();

            var accessKeyId = Environment.GetEnvironmentVariable(EnvAccessKeyId) ?? "Q3AM3UQ867SPQQA43P2F";
            var secretAccessKey = Environment.GetEnvironmentVariable(EnvSecretAccessKey) ?? "zuf+tfteSlswRu7BJ86wekitnifILbZam1KYY3TG";
            var region = Environment.GetEnvironmentVariable(EnvDefaultRegion) ?? "us-east-1";
            var serviceURL = Environment.GetEnvironmentVariable(EnvServiceURL) ?? "http://localhost:9000";
            // var compress = Convert.ToBoolean(Environment.GetEnvironmentVariable(EnvCompress));

            var config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(region),
                ServiceURL = serviceURL,
                ForcePathStyle = true
            };

            Client = new AmazonS3Client(accessKeyId, secretAccessKey, config);
            Uri = MinioS3Utility.GetBucketPath(BucketName, serviceURL);

            FileSystem = new AmazonS3FileSystem(LocalCache,
                                                Uri,
                                                Uri,
                                                Client,
                                                BucketName,
                                                ServerSideEncryptionMethod.None,
                                                null,
                                                Compress);

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