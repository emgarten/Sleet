using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class PackageSetTests
    {
        [Fact]
        public async Task AddPackageAsync_AddsIdentityAndExistsFindsIt()
        {
            var set = new PackageSet();
            var package = CreateInput("PackageA", "1.0.0");

            await set.AddPackageAsync(package);

            set.Exists(package.Identity).Should().BeTrue();
            (await set.GetPackagesAsync()).Should().Contain(package.Identity);
        }

        [Fact]
        public async Task GetPackagesByIdAsync_MatchesIdCaseInsensitivelyAndSortsVersions()
        {
            var set = new PackageSet();
            await set.AddPackagesAsync(new[]
            {
                CreateInput("PackageA", "2.0.0"),
                CreateInput("packagea", "1.0.0"),
                CreateInput("PackageB", "1.0.0")
            });

            var packages = await set.GetPackagesByIdAsync("PACKAGEA");

            packages.Select(e => e.Version.ToNormalizedString()).Should().Equal("1.0.0", "2.0.0");
        }

        [Fact]
        public async Task RemovePackageAsync_RemovesExistingIdentity()
        {
            var set = new PackageSet();
            var package = CreateIdentity("PackageA", "1.0.0");
            await set.AddPackageAsync(PackageInput.CreateForDelete(package, isSymbols: false));

            await set.RemovePackageAsync(package);

            set.Exists(package).Should().BeFalse();
        }

        [Fact]
        public async Task Clone_CopiesPackagesWithoutSharingIndex()
        {
            var set = new PackageSet();
            var packageA = CreateIdentity("PackageA", "1.0.0");
            var packageB = CreateIdentity("PackageB", "1.0.0");
            await set.AddPackageAsync(PackageInput.CreateForDelete(packageA, isSymbols: false));

            var clone = set.Clone();
            await clone.AddPackageAsync(PackageInput.CreateForDelete(packageB, isSymbols: false));

            set.Exists(packageB).Should().BeFalse();
            clone.Exists(packageA).Should().BeTrue();
            clone.Exists(packageB).Should().BeTrue();
        }

        private static PackageInput CreateInput(string id, string version)
        {
            return PackageInput.CreateForDelete(CreateIdentity(id, version), isSymbols: false);
        }

        private static PackageIdentity CreateIdentity(string id, string version)
        {
            return new PackageIdentity(id, NuGetVersion.Parse(version));
        }
    }
}
