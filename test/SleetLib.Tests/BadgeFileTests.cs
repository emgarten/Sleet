using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging.Core;
using NuGet.Test.Helpers;
using NuGet.Versioning;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class BadgeFileTests
    {
        [Fact]
        public async Task BadgeFile_VerifyBadgeForPackage()
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
                        BadgesEnabled = true
                    }
                };

                // Initial packages
                var identities = new HashSet<PackageIdentity>()
                {
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0-a"))

                };

                foreach (var id in identities)
                {
                    var testPackage = new TestNupkg(id.Id, id.Version.ToFullString());
                    var zipFile = testPackage.Save(packagesFolder.Root);
                }

                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { packagesFolder.Root }, false, false, context.Log);

                // Validate
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);
                validateOutput.Should().BeTrue();

                // read output
                var stablePathJson = Path.Combine(target.Root, "badges/v/a.json");
                var prePathJson = Path.Combine(target.Root, "badges/vpre/a.json");
                File.Exists(stablePathJson).Should().BeTrue();
                File.Exists(prePathJson).Should().BeTrue();

                File.ReadAllText(stablePathJson).Should().Contain("1.0.0-a");
                File.ReadAllText(prePathJson).Should().Contain("1.0.0-a");
            }
        }

        [Fact]
        public async Task BadgeFile_VerifyBadgesDifferentVersions()
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
                        BadgesEnabled = true
                    }
                };

                // Initial packages
                var identities = new HashSet<PackageIdentity>()
                {
                    new PackageIdentity("a", NuGetVersion.Parse("3.0.0-a")),
                    new PackageIdentity("a", NuGetVersion.Parse("2.0.0"))
                };

                foreach (var id in identities)
                {
                    var testPackage = new TestNupkg(id.Id, id.Version.ToFullString());
                    var zipFile = testPackage.Save(packagesFolder.Root);
                }

                // Push
                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { packagesFolder.Root }, false, false, context.Log);

                // Validate
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);
                validateOutput.Should().BeTrue();

                // read output
                var stablePathJson = Path.Combine(target.Root, "badges/v/a.json");
                var prePathJson = Path.Combine(target.Root, "badges/vpre/a.json");
                File.Exists(stablePathJson).Should().BeTrue();
                File.Exists(prePathJson).Should().BeTrue();

                File.ReadAllText(stablePathJson).Should().Contain("2.0.0");
                File.ReadAllText(prePathJson).Should().Contain("3.0.0-a");
            }
        }

        [Fact]
        public async Task BadgeFile_VerifyBadgesUpdatedAfterDelete()
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
                        BadgesEnabled = true
                    }
                };

                // Initial packages
                var identities = new HashSet<PackageIdentity>()
                {
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0-a")),
                    new PackageIdentity("a", NuGetVersion.Parse("2.0.0"))
                };

                foreach (var id in identities)
                {
                    var testPackage = new TestNupkg(id.Id, id.Version.ToFullString());
                    var zipFile = testPackage.Save(packagesFolder.Root);
                }

                // Push
                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { packagesFolder.Root }, false, false, context.Log);

                // Remove
                await DeleteCommand.RunAsync(context.LocalSettings, context.Source, "a", "2.0.0", "test", true, context.Log);

                // Validate
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);
                validateOutput.Should().BeTrue();

                // read output
                var stablePathJson = Path.Combine(target.Root, "badges/v/a.json");
                var prePathJson = Path.Combine(target.Root, "badges/vpre/a.json");
                File.Exists(stablePathJson).Should().BeTrue();
                File.Exists(prePathJson).Should().BeTrue();

                File.ReadAllText(stablePathJson).Should().Contain("1.0.0-a");
                File.ReadAllText(prePathJson).Should().Contain("1.0.0-a");
            }
        }

        [Fact]
        public async Task BadgeFile_VerifyBadgesUpdatedAfterDeleteAll()
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
                        BadgesEnabled = true
                    }
                };

                // Initial packages
                var identities = new HashSet<PackageIdentity>()
                {
                    new PackageIdentity("a", NuGetVersion.Parse("2.0.0"))
                };

                foreach (var id in identities)
                {
                    var testPackage = new TestNupkg(id.Id, id.Version.ToFullString());
                    var zipFile = testPackage.Save(packagesFolder.Root);
                }

                // Push
                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { packagesFolder.Root }, false, false, context.Log);

                // Remove
                await DeleteCommand.RunAsync(context.LocalSettings, context.Source, "a", "2.0.0", "test", true, context.Log);

                // Validate
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);
                validateOutput.Should().BeTrue();

                // read output
                var stablePath = Path.Combine(target.Root, "badges/v/a.svg");
                var prePath = Path.Combine(target.Root, "badges/vpre/a.svg");
                File.Exists(stablePath).Should().BeFalse();
                File.Exists(prePath).Should().BeFalse();

                var stablePathJson = Path.Combine(target.Root, "badges/v/a.json");
                var prePathJson = Path.Combine(target.Root, "badges/vpre/a.json");
                File.Exists(stablePathJson).Should().BeFalse();
                File.Exists(prePathJson).Should().BeFalse();
            }
        }
    }
}