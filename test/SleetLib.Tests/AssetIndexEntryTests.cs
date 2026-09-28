using System;
using AwesomeAssertions;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class AssetIndexEntryTests
    {
        [Fact]
        public void Constructor_WithNullArguments_Throws()
        {
            var uri = new Uri("https://example/package/index.json");

            Assert.Throws<ArgumentNullException>(() => new AssetIndexEntry(null, uri));
            Assert.Throws<ArgumentNullException>(() => new AssetIndexEntry(uri, null));
        }

        [Fact]
        public void Equals_WithSameAssetAndPackageIndex_ReturnsTrueAndMatchingHashCode()
        {
            var left = Create("https://example/assets/a.dll", "https://example/packages/a/index.json");
            var right = Create("https://example/assets/a.dll", "https://example/packages/a/index.json");

            left.Equals(right).Should().BeTrue();
            left.Equals((object)right).Should().BeTrue();
            left.GetHashCode().Should().Be(right.GetHashCode());
            (left == right).Should().BeTrue();
            (left != right).Should().BeFalse();
        }

        [Fact]
        public void Equals_WithDifferentPackageIndex_ReturnsFalse()
        {
            var left = Create("https://example/assets/a.dll", "https://example/packages/a/index.json");
            var right = Create("https://example/assets/a.dll", "https://example/packages/b/index.json");

            left.Equals(right).Should().BeFalse();
            left.Equals(null).Should().BeFalse();
            left.Equals(new object()).Should().BeFalse();
        }

        [Fact]
        public void CompareTo_OrdersByAssetUriOrdinalAndIgnoresPackageIndex()
        {
            var upper = Create("https://example/assets/A.dll", "https://example/packages/z/index.json");
            var lower = Create("https://example/assets/a.dll", "https://example/packages/a/index.json");

            upper.CompareTo(lower).Should().BeNegative();
            lower.CompareTo(upper).Should().BePositive();
            lower.CompareTo(null).Should().BePositive();
            (upper < lower).Should().BeTrue();
            (upper <= lower).Should().BeTrue();
            (lower > upper).Should().BeTrue();
            (lower >= upper).Should().BeTrue();
        }

        [Fact]
        public void Operators_WithNulls_FollowExpectedRules()
        {
            AssetIndexEntry nullEntry = null;
            var entry = Create("https://example/assets/a.dll", "https://example/packages/a/index.json");

            (nullEntry == null).Should().BeTrue();
            (nullEntry != entry).Should().BeTrue();
            (nullEntry < entry).Should().BeTrue();
            (nullEntry <= entry).Should().BeTrue();
            (entry > nullEntry).Should().BeTrue();
            (entry >= nullEntry).Should().BeTrue();
        }

        [Fact]
        public void ToString_ReturnsAssetAbsoluteUri()
        {
            var entry = Create("https://example/assets/a.dll", "https://example/packages/a/index.json");

            entry.ToString().Should().Be("https://example/assets/a.dll");
        }

        private static AssetIndexEntry Create(string asset, string packageIndex)
        {
            return new AssetIndexEntry(new Uri(asset), new Uri(packageIndex));
        }
    }
}
