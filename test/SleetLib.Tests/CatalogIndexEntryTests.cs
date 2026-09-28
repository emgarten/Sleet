using System;
using AwesomeAssertions;
using NuGet.Versioning;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class CatalogIndexEntryTests
    {
        [Fact]
        public void Equals_WithSamePackageIdentity_IgnoresUriCommitTimeAndOperation()
        {
            var left = Create("PackageA", "1.0.0", DateTimeOffset.Parse("2024-01-01T00:00:00Z"), SleetOperation.Add, "https://example/a.json");
            var right = Create("PackageA", "1.0.0", DateTimeOffset.Parse("2024-02-01T00:00:00Z"), SleetOperation.Remove, "https://example/b.json");

            left.Equals(right).Should().BeTrue();
            left.Equals((object)right).Should().BeTrue();
            left.GetHashCode().Should().Be(right.GetHashCode());
            (left == right).Should().BeTrue();
            (left != right).Should().BeFalse();
        }

        [Fact]
        public void Equals_WithDifferentPackageIdentity_ReturnsFalse()
        {
            var left = Create("PackageA", "1.0.0", DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
            var right = Create("PackageA", "2.0.0", DateTimeOffset.Parse("2024-01-01T00:00:00Z"));

            left.Equals(right).Should().BeFalse();
            left.Equals(null).Should().BeFalse();
            left.Equals(new object()).Should().BeFalse();
        }

        [Fact]
        public void CompareTo_OrdersByCommitTime()
        {
            var older = Create("PackageA", "1.0.0", DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
            var newer = Create("PackageB", "1.0.0", DateTimeOffset.Parse("2024-01-02T00:00:00Z"));

            older.CompareTo(newer).Should().BeNegative();
            newer.CompareTo(older).Should().BePositive();
            older.CompareTo(null).Should().BePositive();
            (older < newer).Should().BeTrue();
            (older <= newer).Should().BeTrue();
            (newer > older).Should().BeTrue();
            (newer >= older).Should().BeTrue();
        }

        [Fact]
        public void Operators_WithNulls_FollowExpectedRules()
        {
            CatalogIndexEntry nullEntry = null;
            var entry = Create("PackageA", "1.0.0", DateTimeOffset.Parse("2024-01-01T00:00:00Z"));

            (nullEntry == null).Should().BeTrue();
            (nullEntry != entry).Should().BeTrue();
            (nullEntry < entry).Should().BeTrue();
            (nullEntry <= entry).Should().BeTrue();
            (entry > nullEntry).Should().BeTrue();
            (entry >= nullEntry).Should().BeTrue();
        }

        [Fact]
        public void ToString_IncludesOperationIdAndVersion()
        {
            var entry = Create("PackageA", "1.2.3", DateTimeOffset.Parse("2024-01-01T00:00:00Z"), SleetOperation.Remove);

            entry.ToString().Should().Be("Remove PackageA 1.2.3");
        }

        private static CatalogIndexEntry Create(string id, string version, DateTimeOffset commitTime, SleetOperation operation = SleetOperation.Add, string uri = "https://example/package.json")
        {
            return new CatalogIndexEntry(new Uri(uri), id, NuGetVersion.Parse(version), commitTime, operation);
        }
    }
}
