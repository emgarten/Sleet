using System.Collections.Generic;
using System.IO;
using System.Linq;
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

                // read output
                var stablePath = Path.Combine(target.Root, "badges/v/a.svg");
                var prePath = Path.Combine(target.Root, "badges/vpre/a.svg");
                File.Exists(stablePath).Should().BeTrue();
                File.Exists(prePath).Should().BeTrue();
            }
        }
    }
}