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
    public class RetentionPruneCommandTests
    {
        [Fact]
        public async Task RetentionPruneCommand_RemovesAdditionalPackages()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                    {
                        CatalogEnabled = true,
                        SymbolsEnabled = true
                    }
                };

                var identities = new HashSet<PackageIdentity>()
                {
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("3.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("4.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("5.0.0")),
                };

                foreach (var id in identities)
                {
                    var testPackage = new TestNupkg(id.Id, id.Version.ToFullString());
                    var zipFile = testPackage.Save(packagesFolder.Root);
                }

                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { packagesFolder.Root }, false, false, context.Log);

                var pruneContext = new RetentionPruneCommandContext()
                {
                    StableVersionMax = 3,
                    PrereleaseVersionMax = 1
                };

                // Run prune
                await RetentionPruneCommand.PrunePackages(context, pruneContext);

                // Validate
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                // read output
                var packageIndex = new PackageIndex(context);
                var indexPackages = await packageIndex.GetPackagesAsync();

                // Assert
                indexPackages.Count().Should().Be(3);
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("5.0.0"))).Should().BeTrue();
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("4.0.0"))).Should().BeTrue();
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("3.0.0"))).Should().BeTrue();
            }
        }

        [Fact]
        public async Task RetentionPruneCommand_NoopsWhenNoPackagesNeedToBeRemoved()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                    {
                        CatalogEnabled = true,
                        SymbolsEnabled = true
                    }
                };

                var identities = new HashSet<PackageIdentity>()
                {
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("3.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("4.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("5.0.0")),
                };

                foreach (var id in identities)
                {
                    var testPackage = new TestNupkg(id.Id, id.Version.ToFullString());
                    var zipFile = testPackage.Save(packagesFolder.Root);
                }

                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { packagesFolder.Root }, false, false, context.Log);

                var pruneContext = new RetentionPruneCommandContext()
                {
                    StableVersionMax = 10,
                    PrereleaseVersionMax = 10
                };

                // Run prune
                await RetentionPruneCommand.PrunePackages(context, pruneContext);

                // Validate
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                // read output
                var packageIndex = new PackageIndex(context);
                var indexPackages = await packageIndex.GetPackagesAsync();

                // Assert
                indexPackages.Count().Should().Be(5);
            }
        }

        [Fact]
        public async Task RetentionPruneCommand_DoesNotRemovePackagesOnDryRun()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                    {
                        CatalogEnabled = true,
                        SymbolsEnabled = true,
                    }
                };

                var identities = new HashSet<PackageIdentity>()
                {
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("3.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("4.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("5.0.0")),
                };

                foreach (var id in identities)
                {
                    var testPackage = new TestNupkg(id.Id, id.Version.ToFullString());
                    var zipFile = testPackage.Save(packagesFolder.Root);
                }

                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { packagesFolder.Root }, false, false, context.Log);

                var pruneContext = new RetentionPruneCommandContext()
                {
                    StableVersionMax = 1,
                    PrereleaseVersionMax = 1,
                    DryRun = true
                };

                // Run prune
                await RetentionPruneCommand.PrunePackages(context, pruneContext);

                // Validate
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                // read output
                var packageIndex = new PackageIndex(context);
                var indexPackages = await packageIndex.GetPackagesAsync();

                // Assert
                indexPackages.Count().Should().Be(5);
            }
        }

        [Fact]
        public async Task RetentionPruneCommand_PrunesOnPush()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var packagesFolder2 = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                    {
                        CatalogEnabled = true,
                        SymbolsEnabled = true,
                        RetentionMaxStableVersions = 2,
                        RetentionMaxPrereleaseVersions = 2
                    }
                };

                // Initial packages
                var identities = new HashSet<PackageIdentity>()
                {
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("3.0.0")),
                    new PackageIdentity("b", NuGetVersion.Parse("1.0.0")),
                    new PackageIdentity("b", NuGetVersion.Parse("2.0.0")),
                    new PackageIdentity("b", NuGetVersion.Parse("3.0.0")),

                };

                foreach (var id in identities)
                {
                    var testPackage = new TestNupkg(id.Id, id.Version.ToFullString());
                    var zipFile = testPackage.Save(packagesFolder.Root);
                }

                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { packagesFolder.Root }, false, false, context.Log);

                // Second push
                // Initial packages
                identities = new HashSet<PackageIdentity>()
                {
                    new PackageIdentity("a", NuGetVersion.Parse("4.0.0")),
                };

                foreach (var id in identities)
                {
                    var testPackage = new TestNupkg(id.Id, id.Version.ToFullString());
                    var zipFile = testPackage.Save(packagesFolder2.Root);
                }

                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { packagesFolder2.Root }, false, false, context.Log);

                // Validate
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                // read output
                var packageIndex = new PackageIndex(context);
                var indexPackages = await packageIndex.GetPackagesAsync();

                // Assert
                indexPackages.Count().Should().Be(5);
                // new package should exist
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("4.0.0"))).Should().BeTrue();
                // a max of 2 a's should exist
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("3.0.0"))).Should().BeTrue();

                // b should not be impacted by a's push
                indexPackages.Contains(new PackageIdentity("b", NuGetVersion.Parse("3.0.0"))).Should().BeTrue();
                indexPackages.Contains(new PackageIdentity("b", NuGetVersion.Parse("2.0.0"))).Should().BeTrue();
                indexPackages.Contains(new PackageIdentity("b", NuGetVersion.Parse("1.0.0"))).Should().BeTrue();
            }
        }

        [Fact]
        public async Task RetentionPruneCommand_PruneWithFeedOptions()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var packagesFolder2 = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                    {
                        CatalogEnabled = true,
                        SymbolsEnabled = true,
                        RetentionMaxStableVersions = 2,
                        RetentionMaxPrereleaseVersions = 2
                    }
                };

                // Initial packages
                var identities = new HashSet<PackageIdentity>()
                {
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("3.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("4.0.0")),
                    new PackageIdentity("b", NuGetVersion.Parse("1.0.0")),
                    new PackageIdentity("b", NuGetVersion.Parse("2.0.0")),
                    new PackageIdentity("b", NuGetVersion.Parse("3.0.0")),

                };

                foreach (var id in identities)
                {
                    var testPackage = new TestNupkg(id.Id, id.Version.ToFullString());
                    var zipFile = testPackage.Save(packagesFolder.Root);
                }

                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { packagesFolder.Root }, false, false, context.Log);

                // Empty all settings come from the feed.
                var pruneContext = new RetentionPruneCommandContext();

                // Run prune
                await RetentionPruneCommand.PrunePackages(context, pruneContext);

                // Validate
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                // read output
                var packageIndex = new PackageIndex(context);
                var indexPackages = await packageIndex.GetPackagesAsync();

                // Assert
                indexPackages.Count().Should().Be(4);
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("4.0.0"))).Should().BeTrue();
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("3.0.0"))).Should().BeTrue();
                indexPackages.Contains(new PackageIdentity("b", NuGetVersion.Parse("3.0.0"))).Should().BeTrue();
                indexPackages.Contains(new PackageIdentity("b", NuGetVersion.Parse("2.0.0"))).Should().BeTrue();
            }
        }

        [Fact]
        public async Task RetentionPruneCommand_PrunesShouldNotRemoveThePushedPackage()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var packagesFolder2 = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                    {
                        CatalogEnabled = true,
                        SymbolsEnabled = true,
                        RetentionMaxStableVersions = 2,
                        RetentionMaxPrereleaseVersions = 2
                    }
                };

                // Initial packages
                var identities = new HashSet<PackageIdentity>()
                {
                    new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                    new PackageIdentity("a", NuGetVersion.Parse("3.0.0"))

                };

                foreach (var id in identities)
                {
                    var testPackage = new TestNupkg(id.Id, id.Version.ToFullString());
                    var zipFile = testPackage.Save(packagesFolder.Root);
                }

                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { packagesFolder.Root }, false, false, context.Log);

                // Second push
                // Initial packages
                identities = new HashSet<PackageIdentity>()
                {
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                };

                foreach (var id in identities)
                {
                    var testPackage = new TestNupkg(id.Id, id.Version.ToFullString());
                    var zipFile = testPackage.Save(packagesFolder2.Root);
                }

                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { packagesFolder2.Root }, false, false, context.Log);

                // Validate
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                // read output
                var packageIndex = new PackageIndex(context);
                var indexPackages = await packageIndex.GetPackagesAsync();

                // Assert
                indexPackages.Count().Should().Be(3);
                // new package should exist
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("1.0.0"))).Should().BeTrue();
            }
        }

        [Fact]
        public async Task RetentionPruneCommand_PrunesOnPushWithMultiplePushes()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var packagesFolder2 = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                    {
                        CatalogEnabled = true,
                        SymbolsEnabled = true,
                        RetentionMaxStableVersions = 2,
                        RetentionMaxPrereleaseVersions = 1
                    }
                };

                // Initial packages
                var identities = new List<PackageIdentity>()
                {
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0-alpha")),
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta")),
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),

                    new PackageIdentity("b", NuGetVersion.Parse("1.0.0-alpha")),
                    new PackageIdentity("b", NuGetVersion.Parse("1.0.0-beta")),
                    new PackageIdentity("b", NuGetVersion.Parse("1.0.0")),

                    new PackageIdentity("a", NuGetVersion.Parse("2.0.0-alpha")),
                    new PackageIdentity("a", NuGetVersion.Parse("2.0.0-beta")),
                    new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),

                    new PackageIdentity("b", NuGetVersion.Parse("2.0.0-alpha")),
                    new PackageIdentity("b", NuGetVersion.Parse("2.0.0-beta")),
                    new PackageIdentity("b", NuGetVersion.Parse("2.0.0")),

                    new PackageIdentity("a", NuGetVersion.Parse("3.0.0-alpha")),
                    new PackageIdentity("a", NuGetVersion.Parse("3.0.0-beta")),
                    new PackageIdentity("a", NuGetVersion.Parse("3.0.0")),

                    new PackageIdentity("b", NuGetVersion.Parse("3.0.0-alpha")),
                    new PackageIdentity("b", NuGetVersion.Parse("3.0.0-beta")),
                    new PackageIdentity("b", NuGetVersion.Parse("3.0.0")),

                };

                await InitCommand.InitAsync(context);

                // Push packages 1 at a time
                foreach (var id in identities)
                {
                    var testPackage = new TestNupkg(id.Id, id.Version.ToFullString());
                    var zipFile = testPackage.Save(packagesFolder.Root);
                    await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile.FullName }, false, false, context.Log);
                }

                // Validate
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                // read output
                var packageIndex = new PackageIndex(context);
                var indexPackages = await packageIndex.GetPackagesAsync();

                // Assert
                indexPackages.Count().Should().Be(6);
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("3.0.0"))).Should().BeTrue();
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("2.0.0"))).Should().BeTrue();
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("3.0.0-beta"))).Should().BeTrue();

                indexPackages.Contains(new PackageIdentity("b", NuGetVersion.Parse("3.0.0"))).Should().BeTrue();
                indexPackages.Contains(new PackageIdentity("b", NuGetVersion.Parse("2.0.0"))).Should().BeTrue();
                indexPackages.Contains(new PackageIdentity("b", NuGetVersion.Parse("3.0.0-beta"))).Should().BeTrue();
            }
        }

        [Fact]
        public async Task RetentionPruneCommand_RemovesAdditionalPackages_BasedOnReleaseLabels()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                    {
                        CatalogEnabled = true,
                        SymbolsEnabled = true
                    }
                };

                var identities = new HashSet<PackageIdentity>()
                {
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.1")),
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.2")),
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.1")),
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.2")),
                };

                foreach (var id in identities)
                {
                    var testPackage = new TestNupkg(id.Id, id.Version.ToFullString());
                    var zipFile = testPackage.Save(packagesFolder.Root);
                }

                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { packagesFolder.Root }, false, false, context.Log);

                var pruneContext = new RetentionPruneCommandContext()
                {
                    StableVersionMax = 1024,
                    PrereleaseVersionMax = 1,
                    GroupByFirstPrereleaseLabelCount = 2
                };

                // Run prune
                await RetentionPruneCommand.PrunePackages(context, pruneContext);

                // Validate
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                // read output
                var packageIndex = new PackageIndex(context);
                var indexPackages = await packageIndex.GetPackagesAsync();

                // Assert
                indexPackages.Count().Should().Be(2);
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.2"))).Should().BeTrue();
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.2"))).Should().BeTrue();
            }
        }

        [Fact]
        public async Task RetentionPruneCommand_RunAsyncGivenPackagesVerifyLocksPrunesAndCommits()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();
                var context = new SleetContext()
                {
                    Token = TestContext.Current.CancellationToken,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                    {
                        CatalogEnabled = true,
                        SymbolsEnabled = true
                    }
                };

                for (var i = 1; i <= 4; i++)
                {
                    new TestNupkg("a", $"{i}.0.0").Save(packagesFolder.Root);
                }

                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);

                var pruneContext = new RetentionPruneCommandContext()
                {
                    StableVersionMax = 2,
                    PrereleaseVersionMax = 1
                };

                var success = await RetentionPruneCommand.RunAsync(settings, fileSystem, pruneContext, log);

                var packageIndex = new PackageIndex(context);
                var indexPackages = await packageIndex.GetPackagesAsync();

                success.Should().BeTrue();
                indexPackages.Count().Should().Be(2);
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("4.0.0"))).Should().BeTrue();
                indexPackages.Contains(new PackageIdentity("a", NuGetVersion.Parse("3.0.0"))).Should().BeTrue();
                log.GetMessages().Should().Contain($"Pruning packages in {fileSystem.BaseURI}");
                log.GetMessages().Should().Contain("Pruning a.2.0.0");
                log.GetMessages().Should().Contain("Successfully pruned packages.");
            }
        }

        [Theory]
        [InlineData(0, 1, "Package retention must specify a maximum number of stable versions that is > 0")]
        [InlineData(1, 0, "Package retention must specify a maximum number of prerelease versions that is > 0")]
        public async Task RetentionPruneCommand_RunAsyncGivenInvalidMaximumsVerifyRejected(int stableMax, int prereleaseMax, string message)
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();
                var pruneContext = new RetentionPruneCommandContext()
                {
                    StableVersionMax = stableMax,
                    PrereleaseVersionMax = prereleaseMax
                };

                await InitCommand.RunAsync(settings, fileSystem, log);

                Func<Task> action = async () => await RetentionPruneCommand.RunAsync(settings, fileSystem, pruneContext, log);

                await action.Should().ThrowAsync<ArgumentException>().WithMessage(message);
            }
        }

        [Fact]
        public async Task RetentionPruneCommand_RunAsyncGivenDryRunVerifyLogsButDoesNotRemovePackages()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();
                var context = new SleetContext()
                {
                    Token = TestContext.Current.CancellationToken,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                    {
                        CatalogEnabled = true,
                        SymbolsEnabled = true
                    }
                };

                for (var i = 1; i <= 3; i++)
                {
                    new TestNupkg("a", $"{i}.0.0").Save(packagesFolder.Root);
                }

                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);

                var pruneContext = new RetentionPruneCommandContext()
                {
                    StableVersionMax = 1,
                    PrereleaseVersionMax = 1,
                    DryRun = true
                };

                var success = await RetentionPruneCommand.RunAsync(settings, fileSystem, pruneContext, log);
                var packageIndex = new PackageIndex(context);
                var indexPackages = await packageIndex.GetPackagesAsync();

                success.Should().BeTrue();
                indexPackages.Count().Should().Be(3);
                log.GetMessages().Should().Contain("Pruning a.2.0.0");
                log.GetMessages().Should().Contain("Pruning a.1.0.0");
            }
        }
    }
}