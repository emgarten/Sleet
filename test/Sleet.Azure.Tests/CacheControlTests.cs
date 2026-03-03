using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Sleet.Test.Common;
using System.Net.Http.Headers;

namespace Sleet.Azure.Tests
{
    public class CacheControlTests
    {
        [EnvVarExistsFact(AzureTestContext.EnvVarName)]
        public async Task GivenDefaultSettings_VerifyCacheControlIsNoStore()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AzureTestContext())
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
                var nupkgBlob = testContext.Container.GetBlobClient("flatcontainer/packagea/1.0.0/packagea.1.0.0.nupkg");
                var nupkgProperties = await nupkgBlob.GetPropertiesAsync();
                CacheControlHeaderValue.Parse(nupkgProperties.Value.CacheControl).Should().Be(CacheControlHeaderValue.Parse("no-store"));

                // Verify .nuspec has no-store
                var nuspecBlob = testContext.Container.GetBlobClient("flatcontainer/packagea/1.0.0/packagea.nuspec");
                var nuspecProperties = await nuspecBlob.GetPropertiesAsync();
                CacheControlHeaderValue.Parse(nuspecProperties.Value.CacheControl).Should().Be(CacheControlHeaderValue.Parse("no-store"));

                // Verify index.json has no-store
                var indexBlob = testContext.Container.GetBlobClient("index.json");
                var indexProperties = await indexBlob.GetPropertiesAsync();
                CacheControlHeaderValue.Parse(indexProperties.Value.CacheControl).Should().Be(CacheControlHeaderValue.Parse("no-store"));

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AzureTestContext.EnvVarName)]
        public async Task GivenCustomCacheControl_VerifyHeadersAreSet()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AzureTestContext())
            {
                await testContext.InitAsync();

                var immutableCacheControl = "public, max-age=31536000, immutable";
                var mutableCacheControl = "public, max-age=300, must-revalidate";

                // Create file system with custom cache control
                testContext.FileSystem = new AzureFileSystem(
                    testContext.LocalCache,
                    testContext.Uri,
                    testContext.Uri,
                    testContext.StorageAccount,
                    testContext.ContainerName,
                    feedSubPath: null,
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
                var nupkgBlob = testContext.Container.GetBlobClient("flatcontainer/packageb/1.0.0/packageb.1.0.0.nupkg");
                var nupkgProperties = await nupkgBlob.GetPropertiesAsync();
                CacheControlHeaderValue.Parse(nupkgProperties.Value.CacheControl).Should().Be(CacheControlHeaderValue.Parse(immutableCacheControl));

                // Verify .nuspec has immutable cache control
                var nuspecBlob = testContext.Container.GetBlobClient("flatcontainer/packageb/1.0.0/packageb.nuspec");
                var nuspecProperties = await nuspecBlob.GetPropertiesAsync();
                CacheControlHeaderValue.Parse(nuspecProperties.Value.CacheControl).Should().Be(CacheControlHeaderValue.Parse(immutableCacheControl));

                // Verify index.json has mutable cache control
                var indexBlob = testContext.Container.GetBlobClient("index.json");
                var indexProperties = await indexBlob.GetPropertiesAsync();
                CacheControlHeaderValue.Parse(indexProperties.Value.CacheControl).Should().Be(CacheControlHeaderValue.Parse(mutableCacheControl));

                // Verify flatcontainer index.json has mutable cache control
                var flatcontainerIndexBlob = testContext.Container.GetBlobClient("flatcontainer/packageb/index.json");
                var flatcontainerIndexProperties = await flatcontainerIndexBlob.GetPropertiesAsync();
                CacheControlHeaderValue.Parse(flatcontainerIndexProperties.Value.CacheControl).Should().Be(CacheControlHeaderValue.Parse(mutableCacheControl));

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AzureTestContext.EnvVarName)]
        public async Task GivenCustomCacheControlViaFactory_VerifyHeadersAreSet()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AzureTestContext())
            {
                await testContext.InitAsync();

                var immutableCacheControl = "public, max-age=604800";
                var mutableCacheControl = "public, max-age=60";

                var settings = LocalSettings.Load(new JObject(
                    new JProperty("sources",
                        new JArray(
                            new JObject(
                                new JProperty("name", "azure"),
                                new JProperty("type", "azure"),
                                new JProperty("container", testContext.ContainerName),
                                new JProperty("connectionString", AzureTestContext.GetConnectionString()),
                                new JProperty("immutableCacheControl", immutableCacheControl),
                                new JProperty("mutableCacheControl", mutableCacheControl))))));

                var fs = await FileSystemFactory.CreateFileSystemAsync(settings, testContext.LocalCache, "azure", testContext.Logger);

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
                var nupkgBlob = testContext.Container.GetBlobClient("flatcontainer/packagec/2.0.0/packagec.2.0.0.nupkg");
                var nupkgProperties = await nupkgBlob.GetPropertiesAsync();
                CacheControlHeaderValue.Parse(nupkgProperties.Value.CacheControl).Should().Be(CacheControlHeaderValue.Parse(immutableCacheControl));

                // Verify index.json has mutable cache control
                var indexBlob = testContext.Container.GetBlobClient("index.json");
                var indexProperties = await indexBlob.GetPropertiesAsync();
                CacheControlHeaderValue.Parse(indexProperties.Value.CacheControl).Should().Be(CacheControlHeaderValue.Parse(mutableCacheControl));

                await testContext.CleanupAsync();
            }
        }
    }
}
