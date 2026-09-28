using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using NuGet.Packaging.Core;
using NuGet.Test.Helpers;
using NuGet.Versioning;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class DeleteCommandTests
    {
        [Fact]
        public async Task DeleteCommand_GivenExistingPackageVersionVerifyVersionDeleted()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                await InitCommand.RunAsync(settings, fileSystem, enableCatalog: true, enableSymbols: false, log: log, token: TestContext.Current.CancellationToken);
                new TestNupkg("a", "1.0.0").Save(packagesFolder.Root);
                new TestNupkg("a", "2.0.0").Save(packagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);

                var success = await DeleteCommand.RunAsync(settings, fileSystem, "a", "1.0.0", "removing", false, log);

                var packageIndex = new PackageIndex(new SleetContext()
                {
                    Token = TestContext.Current.CancellationToken,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = await FeedSettingsUtility.GetSettingsOrDefault(fileSystem, log, TestContext.Current.CancellationToken)
                });
                var packages = await packageIndex.GetPackagesAsync();

                success.Should().BeTrue();
                packages.Should().BeEquivalentTo(new[] { new PackageIdentity("a", NuGetVersion.Parse("2.0.0")) });
                log.GetMessages().Should().Contain("Removing a.1.0.0");
            }
        }

        [Fact]
        public async Task DeleteCommand_GivenPackageIdWithoutVersionVerifyAllVersionsDeleted()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                await InitCommand.RunAsync(settings, fileSystem, enableCatalog: true, enableSymbols: false, log: log, token: TestContext.Current.CancellationToken);
                new TestNupkg("a", "1.0.0").Save(packagesFolder.Root);
                new TestNupkg("a", "2.0.0").Save(packagesFolder.Root);
                new TestNupkg("b", "1.0.0").Save(packagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);

                var success = await DeleteCommand.RunAsync(settings, fileSystem, "a", null, null, false, log);

                var packageIndex = new PackageIndex(new SleetContext()
                {
                    Token = TestContext.Current.CancellationToken,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = await FeedSettingsUtility.GetSettingsOrDefault(fileSystem, log, TestContext.Current.CancellationToken)
                });
                var packages = await packageIndex.GetPackagesAsync();

                success.Should().BeTrue();
                packages.Should().BeEquivalentTo(new[] { new PackageIdentity("b", NuGetVersion.Parse("1.0.0")) });
            }
        }

        [Fact]
        public async Task DeleteCommand_GivenMissingPackageWithoutForceVerifyThrows()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                await InitCommand.RunAsync(settings, fileSystem, log);

                Func<Task> action = async () => await DeleteCommand.RunAsync(settings, fileSystem, "missing", "1.0.0", null, false, log);

                await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Package does not exists: missing.1.0.0");
                log.GetMessages().Should().Contain("missing.1.0.0 does not exist.");
            }
        }

        [Fact]
        public async Task DeleteCommand_GivenMissingPackageWithForceVerifyIgnored()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                await InitCommand.RunAsync(settings, fileSystem, log);

                var success = await DeleteCommand.RunAsync(settings, fileSystem, "missing", "1.0.0", null, true, log);

                success.Should().BeTrue();
                log.GetMessages().Should().Contain("missing.1.0.0 does not exist.");
                log.GetMessages().Should().Contain("Successfully deleted packages.");
            }
        }

        [Fact]
        public async Task DeleteCommand_GivenSymbolsPackageVerifySymbolsDeleted()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                await InitCommand.RunAsync(settings, fileSystem, enableCatalog: true, enableSymbols: true, log: log, token: TestContext.Current.CancellationToken);
                var package = new TestNupkg("a", "1.0.0");
                var symbols = new TestNupkg("a", "1.0.0");
                symbols.Nuspec.IsSymbolPackage = true;
                package.Save(packagesFolder.Root);
                symbols.Save(packagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);

                var success = await DeleteCommand.RunAsync(settings, fileSystem, "a", "1.0.0", null, false, log);

                var packageIndex = new PackageIndex(new SleetContext()
                {
                    Token = TestContext.Current.CancellationToken,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = await FeedSettingsUtility.GetSettingsOrDefault(fileSystem, log, TestContext.Current.CancellationToken)
                });

                success.Should().BeTrue();
                (await packageIndex.GetPackagesAsync()).Should().BeEmpty();
                (await packageIndex.GetSymbolsPackagesAsync()).Should().BeEmpty();
                log.GetMessages().Should().Contain("Removing a.1.0.0 and symbols package for a.1.0.0");
            }
        }
    }
}
