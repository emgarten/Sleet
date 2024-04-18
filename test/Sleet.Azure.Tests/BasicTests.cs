using FluentAssertions;
using NuGet.Test.Helpers;
using Sleet.Test.Common;

namespace Sleet.Azure.Tests
{
    /// <summary>
    /// These tests can run locally against developer storage by changing
    /// EnvVarExistsFactAttribute -> Fact and starting up the emulator.
    /// </summary>
    public class BasicTests
    {
        [EnvVarExistsFact(AzureTestContext.EnvVarName)]
        public async Task GivenAStorageAccountVerifyInitSucceeds()
        {
            using (var testContext = new AzureTestContext())
            {
                await testContext.InitAsync();

                var result = await InitCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.Logger,
                    token: CancellationToken.None);

                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    testContext.Logger);

                result.Should().BeTrue();

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AzureTestContext.EnvVarName)]
        public async Task GivenAStorageAccountVerifyPushSucceeds()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AzureTestContext())
            {
                await testContext.InitAsync();

                var testPackage = new TestNupkg("packageA", "1.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);

                var result = await InitCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.Logger,
                    token: CancellationToken.None);

                result &= await PushCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    new List<string>() { zipFile.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.Logger);

                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    testContext.Logger);

                result.Should().BeTrue();

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AzureTestContext.EnvVarName)]
        public async Task GivenAStorageAccountWithNoContainerVerifyPushSucceeds()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AzureTestContext())
            {
                // Skip creation and allow it to be done during push.
                testContext.CreateContainerOnInit = false;

                await testContext.InitAsync();

                var testPackage = new TestNupkg("packageA", "1.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);

                var result = await PushCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    new List<string>() { zipFile.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.Logger);

                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    testContext.Logger);

                result.Should().BeTrue();

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AzureTestContext.EnvVarName)]
        public async Task GivenAStorageAccountWithNoInitVerifyPushSucceeds()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AzureTestContext())
            {
                await testContext.InitAsync();

                var testPackage = new TestNupkg("packageA", "1.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);

                // Skip init
                var result = await PushCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    new List<string>() { zipFile.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.Logger);

                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    testContext.Logger);

                result.Should().BeTrue();

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AzureTestContext.EnvVarName)]
        public async Task GivenAStorageAccountVerifyPushAndRemoveSucceed()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AzureTestContext())
            {
                await testContext.InitAsync();

                var testPackage = new TestNupkg("packageA", "1.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);

                var result = await InitCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.Logger,
                    token: CancellationToken.None);

                result &= await PushCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    new List<string>() { zipFile.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.Logger);

                result &= await DeleteCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    "packageA",
                    "1.0.0",
                    "test",
                    force: true,
                    log: testContext.Logger);

                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    testContext.Logger);

                result.Should().BeTrue();

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AzureTestContext.EnvVarName)]
        public async Task GivenAStorageAccountVerifyPushWithBaseURI()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AzureTestContext())
            {
                var baseUri = new Uri("http://tempuri.org/abc/");
                var fileSystem = new AzureFileSystem(testContext.LocalCache, testContext.Uri, baseUri, testContext.StorageAccount, testContext.ContainerName);
                testContext.FileSystem = fileSystem;

                await testContext.InitAsync();

                var testPackage = new TestNupkg("packageA", "1.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);

                var result = await InitCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.Logger,
                    token: CancellationToken.None);

                result &= await PushCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    new List<string>() { zipFile.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.Logger);

                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    testContext.Logger);

                result.Should().BeTrue();

                // Check baseURIs
                await BaseURITestUtil.VerifyBaseUris(testContext.FileSystem.Files.Values, baseUri);

                await testContext.CleanupAsync();
            }
        }
    }
}
