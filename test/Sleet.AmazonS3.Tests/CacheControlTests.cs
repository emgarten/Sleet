using Amazon.S3.Model;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Sleet.Test.Common;

namespace Sleet.AmazonS3.Tests
{
    public class CacheControlTests
    {
        [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task GivenDefaultSettings_VerifyCacheControlIsNoStore()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AmazonS3TestContext())
            {
                await testContext.InitAsync();

                // Create a test package
                var testPackage = new TestNupkg("packageA", "1.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);

                // Push package
                await PushCommand.RunAsync(
                    testContext.LocalSettings,
                    testContext.FileSystem,
                    new List<string> { zipFile.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.Logger);

                // Verify .nupkg has no-store
                var nupkgMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "flatcontainer/packagea/1.0.0/packagea.1.0.0.nupkg");
                nupkgMetadata.Headers.CacheControl.Should().Be("no-store");

                // Verify .nuspec has no-store
                var nuspecMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "flatcontainer/packagea/1.0.0/packagea.nuspec");
                nuspecMetadata.Headers.CacheControl.Should().Be("no-store");

                // Verify index.json has no-store
                var indexMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "index.json");
                indexMetadata.Headers.CacheControl.Should().Be("no-store");

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task GivenCustomCacheControl_VerifyHeadersAreSet()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AmazonS3TestContext())
            {
                await testContext.InitAsync();

                var immutableCacheControl = "public, max-age=31536000, immutable";
                var mutableCacheControl = "public, max-age=300, must-revalidate";

                // Create file system with custom cache control
                testContext.FileSystem = new AmazonS3FileSystem(
                    testContext.LocalCache,
                    testContext.Uri,
                    testContext.Uri,
                    testContext.Client,
                    testContext.BucketName,
                    Amazon.S3.ServerSideEncryptionMethod.None,
                    feedSubPath: null,
                    compress: true,
                    acl: null,
                    disablePayloadSigning: false,
                    immutableCacheControl: immutableCacheControl,
                    mutableCacheControl: mutableCacheControl);

                // Initialize feed
                await InitCommand.RunAsync(
                    testContext.LocalSettings,
                    testContext.FileSystem,
                    enableCatalog: false,
                    enableSymbols: false,
                    log: testContext.Logger,
                    token: CancellationToken.None);

                // Create a test package
                var testPackage = new TestNupkg("packageB", "1.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);

                // Push package
                await PushCommand.RunAsync(
                    testContext.LocalSettings,
                    testContext.FileSystem,
                    new List<string> { zipFile.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.Logger);

                // Verify .nupkg has immutable cache control
                var nupkgMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "flatcontainer/packageb/1.0.0/packageb.1.0.0.nupkg");
                nupkgMetadata.Headers.CacheControl.Should().Be(immutableCacheControl);

                // Verify .nuspec has immutable cache control
                var nuspecMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "flatcontainer/packageb/1.0.0/packageb.nuspec");
                nuspecMetadata.Headers.CacheControl.Should().Be(immutableCacheControl);

                // Verify index.json has mutable cache control
                var indexMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "index.json");
                indexMetadata.Headers.CacheControl.Should().Be(mutableCacheControl);

                // Verify flatcontainer index.json has mutable cache control
                var flatcontainerIndexMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "flatcontainer/packageb/index.json");
                flatcontainerIndexMetadata.Headers.CacheControl.Should().Be(mutableCacheControl);

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task GivenCustomCacheControlViaFactory_VerifyHeadersAreSet()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AmazonS3TestContext())
            {
                await testContext.InitAsync();

                var immutableCacheControl = "public, max-age=604800";
                var mutableCacheControl = "public, max-age=60";

                var accessKeyId = Environment.GetEnvironmentVariable(AmazonS3TestContext.EnvAccessKeyId);
                var secretAccessKey = Environment.GetEnvironmentVariable(AmazonS3TestContext.EnvSecretAccessKey);
                var region = Environment.GetEnvironmentVariable(AmazonS3TestContext.EnvDefaultRegion) ?? "us-east-1";

                var settings = LocalSettings.Load(new JObject(
                    new JProperty("sources",
                        new JArray(
                            new JObject(
                                new JProperty("name", "s3"),
                                new JProperty("type", "s3"),
                                new JProperty("bucketName", testContext.BucketName),
                                new JProperty("region", region),
                                new JProperty("accessKeyId", accessKeyId),
                                new JProperty("secretAccessKey", secretAccessKey),
                                new JProperty("immutableCacheControl", immutableCacheControl),
                                new JProperty("mutableCacheControl", mutableCacheControl))))));

                var fs = await FileSystemFactory.CreateFileSystemAsync(settings, testContext.LocalCache, "s3", testContext.Logger);

                // Initialize feed
                await InitCommand.RunAsync(
                    settings,
                    fs,
                    enableCatalog: false,
                    enableSymbols: false,
                    log: testContext.Logger,
                    token: CancellationToken.None);

                // Create a test package
                var testPackage = new TestNupkg("packageC", "2.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);

                // Push package
                await PushCommand.RunAsync(
                    settings,
                    fs,
                    new List<string> { zipFile.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.Logger);

                // Verify .nupkg has immutable cache control
                var nupkgMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "flatcontainer/packagec/2.0.0/packagec.2.0.0.nupkg");
                nupkgMetadata.Headers.CacheControl.Should().Be(immutableCacheControl);

                // Verify index.json has mutable cache control
                var indexMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "index.json");
                indexMetadata.Headers.CacheControl.Should().Be(mutableCacheControl);

                await testContext.CleanupAsync();
            }
        }
    }
}
