using FluentAssertions;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Helpers;
using Sleet.Test.Common;

namespace Sleet.Azure.Tests
{
    /// <summary>
    /// These tests can run locally against developer storage by changing
    /// EnvVarExistsFactAttribute -> Fact and starting up the emulator.
    /// </summary>
    public class AzureNuGetIntegrationTests
    {
        [EnvVarExistsFact(AzureTestContext.EnvVarName)]
        public async Task GivenPushCreatesAContainerVerifyNuGetCanRead()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AzureTestContext())
            using (var sourceContext = new SourceCacheContext())
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

                // Read the feed with NuGet.Protocol
                var feedIndex = $"{testContext.FileSystem.Root.AbsoluteUri}index.json";
                var repo = Repository.Factory.GetCoreV3(feedIndex);
                var resource = await repo.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None);

                var packageResults = (await resource.GetAllVersionsAsync("packageA", sourceContext, NullLogger.Instance, CancellationToken.None)).ToList();
                packageResults.Count.Should().Be(1);
                packageResults[0].ToIdentityString().Should().Be("1.0.0");

                await testContext.CleanupAsync();
            }
        }
    }
}
