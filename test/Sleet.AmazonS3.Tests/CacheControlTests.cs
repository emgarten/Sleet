using AwesomeAssertions;
using NuGet.Test.Helpers;
using System.Net.Http.Headers;

namespace Sleet.AmazonS3.Tests
{
    public class CacheControlTests
    {
        [AmazonS3Fact]
        public async Task GivenDefaultSettings_VerifyCacheControlIsNoStore()
        {
            using (var packagesFolder = new TestFolder())
            await using (var testContext = new AmazonS3TestContext())
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
                CacheControlHeaderValue.Parse(nupkgMetadata.Headers.CacheControl).Should().Be(CacheControlHeaderValue.Parse("no-store"));

                // Verify .nuspec has no-store
                var nuspecMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "flatcontainer/packagea/1.0.0/packagea.nuspec");
                CacheControlHeaderValue.Parse(nuspecMetadata.Headers.CacheControl).Should().Be(CacheControlHeaderValue.Parse("no-store"));

                // Verify index.json has no-store
                var indexMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "index.json");
                CacheControlHeaderValue.Parse(indexMetadata.Headers.CacheControl).Should().Be(CacheControlHeaderValue.Parse("no-store"));

                await testContext.CleanupAsync();
            }
        }

        [AmazonS3Fact]
        public async Task GivenCustomCacheControl_VerifyHeadersAreSet()
        {
            using (var packagesFolder = new TestFolder())
            await using (var testContext = new AmazonS3TestContext())
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
                CacheControlHeaderValue.Parse(nupkgMetadata.Headers.CacheControl).Should().Be(CacheControlHeaderValue.Parse(immutableCacheControl));

                // Verify .nuspec has immutable cache control
                var nuspecMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "flatcontainer/packageb/1.0.0/packageb.nuspec");
                CacheControlHeaderValue.Parse(nuspecMetadata.Headers.CacheControl).Should().Be(CacheControlHeaderValue.Parse(immutableCacheControl));

                // Verify index.json has mutable cache control
                var indexMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "index.json");
                CacheControlHeaderValue.Parse(indexMetadata.Headers.CacheControl).Should().Be(CacheControlHeaderValue.Parse(mutableCacheControl));

                // Verify flatcontainer index.json has mutable cache control
                var flatcontainerIndexMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "flatcontainer/packageb/index.json");
                CacheControlHeaderValue.Parse(flatcontainerIndexMetadata.Headers.CacheControl).Should().Be(CacheControlHeaderValue.Parse(mutableCacheControl));

                await testContext.CleanupAsync();
            }
        }

        [AmazonS3Fact]
        public async Task GivenCustomCacheControlViaFactory_VerifyHeadersAreSet()
        {
            using (var packagesFolder = new TestFolder())
            await using (var testContext = new AmazonS3TestContext())
            {
                await testContext.InitAsync();

                var immutableCacheControl = "public, max-age=604800";
                var mutableCacheControl = "public, max-age=60";

                var fs = await testContext.CreateFileSystemAsync(source =>
                {
                    source.Add("immutableCacheControl", immutableCacheControl);
                    source.Add("mutableCacheControl", mutableCacheControl);
                });

                // Initialize feed
                await InitCommand.RunAsync(
                    testContext.LocalSettings,
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
                    testContext.LocalSettings,
                    fs,
                    new List<string> { zipFile.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.Logger);

                // Verify .nupkg has immutable cache control
                var nupkgMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "flatcontainer/packagec/2.0.0/packagec.2.0.0.nupkg");
                CacheControlHeaderValue.Parse(nupkgMetadata.Headers.CacheControl).Should().Be(CacheControlHeaderValue.Parse(immutableCacheControl));

                // Verify index.json has mutable cache control
                var indexMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "index.json");
                CacheControlHeaderValue.Parse(indexMetadata.Headers.CacheControl).Should().Be(CacheControlHeaderValue.Parse(mutableCacheControl));

                await testContext.CleanupAsync();
            }
        }
    }
}
