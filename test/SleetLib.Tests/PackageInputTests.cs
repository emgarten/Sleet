using AwesomeAssertions;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class PackageInputTests
    {
        [Fact]
        public void Equals_WithSameIdentityAndSymbolsFlag_ReturnsTrueAndMatchingHashCode()
        {
            var left = Create("PackageA", "1.0.0", isSymbols: false);
            var right = Create("PackageA", "1.0.0", isSymbols: false);

            left.Equals(right).Should().BeTrue();
            left.Equals((object)right).Should().BeTrue();
            left.GetHashCode().Should().Be(right.GetHashCode());
            (left == right).Should().BeTrue();
            (left != right).Should().BeFalse();
        }

        [Fact]
        public void Equals_WithDifferentSymbolsFlag_ReturnsFalse()
        {
            var package = Create("PackageA", "1.0.0", isSymbols: false);
            var symbols = Create("PackageA", "1.0.0", isSymbols: true);

            package.Equals(symbols).Should().BeFalse();
            (package == symbols).Should().BeFalse();
            (package != symbols).Should().BeTrue();
        }

        [Fact]
        public void Equals_WithDifferentIdentity_ReturnsFalse()
        {
            var left = Create("PackageA", "1.0.0", isSymbols: false);
            var right = Create("PackageA", "2.0.0", isSymbols: false);

            left.Equals(right).Should().BeFalse();
        }

        [Fact]
        public void CompareTo_OrdersByIdentityThenSymbolsPackageFirst()
        {
            var packageA = Create("PackageA", "1.0.0", isSymbols: false);
            var symbolsA = Create("PackageA", "1.0.0", isSymbols: true);
            var packageB = Create("PackageB", "1.0.0", isSymbols: false);

            symbolsA.CompareTo(packageA).Should().BeNegative();
            packageA.CompareTo(symbolsA).Should().BePositive();
            packageA.CompareTo(packageB).Should().BeNegative();
            packageA.CompareTo(null).Should().BeNegative();
        }

        [Fact]
        public void Operators_WithNulls_FollowCompareAndEqualityRules()
        {
            PackageInput nullPackage = null;
            var package = Create("PackageA", "1.0.0", isSymbols: false);
            var newerPackage = Create("PackageA", "2.0.0", isSymbols: false);

            (nullPackage == null).Should().BeTrue();
            (nullPackage != package).Should().BeTrue();
            (nullPackage < package).Should().BeTrue();
            (nullPackage <= package).Should().BeTrue();
            (package > nullPackage).Should().BeFalse();
            (package >= nullPackage).Should().BeFalse();
            (package < newerPackage).Should().BeTrue();
            (newerPackage > package).Should().BeTrue();
        }

        [Fact]
        public void ToString_IncludesSymbolsSuffixOnlyForSymbolsPackages()
        {
            Create("PackageA", "1.2.3", isSymbols: false).ToString().Should().Be("PackageA 1.2.3");
            Create("PackageA", "1.2.3", isSymbols: true).ToString().Should().Be("PackageA 1.2.3 (Symbols)");
        }

        private static PackageInput Create(string id, string version, bool isSymbols)
        {
            return PackageInput.CreateForDelete(new PackageIdentity(id, NuGetVersion.Parse(version)), isSymbols);
        }
    }
}
