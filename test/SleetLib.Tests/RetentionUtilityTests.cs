using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class RetentionUtilityTests
    {
        [Fact]
        public void RetentionUtility_PruneSinglePackage()
        {
            var feed = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("3.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("4.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("5.0.0")),
            };

            var pinned = new HashSet<PackageIdentity>();

            var expected = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0"))
            };

            var pruned = RetentionUtility.GetPackagesToPrune(feed, pinned, stableVersionMax: 4, prereleaseVersionMax: 4);

            pruned.OrderBy(e => e).Should().BeEquivalentTo(expected.OrderBy(e => e));
        }

        [Fact]
        public void RetentionUtility_PruneAllButOne()
        {
            var feed = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("5.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("5.0.0")),
            };

            var pinned = new HashSet<PackageIdentity>();

            var expected = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta")),
            };

            var pruned = RetentionUtility.GetPackagesToPrune(feed, pinned, stableVersionMax: 1, prereleaseVersionMax: 1);

            pruned.OrderBy(e => e).Should().BeEquivalentTo(expected.OrderBy(e => e));
        }

        [Fact]
        public void RetentionUtility_MultipleIdsAreSeparate()
        {
            var feed = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                new PackageIdentity("b", NuGetVersion.Parse("2.0.0")),
                new PackageIdentity("c", NuGetVersion.Parse("3.0.0")),
                new PackageIdentity("d", NuGetVersion.Parse("4.0.0")),
            };

            var pinned = new HashSet<PackageIdentity>();

            var expected = new HashSet<PackageIdentity>()
            {
            };

            var pruned = RetentionUtility.GetPackagesToPrune(feed, pinned, stableVersionMax: 1, prereleaseVersionMax: 1);

            pruned.OrderBy(e => e).Should().BeEquivalentTo(expected.OrderBy(e => e));
        }

        [Fact]
        public void RetentionUtility_PruneWithMultipleIds()
        {
            var feed = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                new PackageIdentity("b", NuGetVersion.Parse("1.0.0")),
                new PackageIdentity("b", NuGetVersion.Parse("2.0.0")),
            };

            var pinned = new HashSet<PackageIdentity>();

            var expected = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                new PackageIdentity("b", NuGetVersion.Parse("1.0.0")),
            };

            var pruned = RetentionUtility.GetPackagesToPrune(feed, pinned, stableVersionMax: 1, prereleaseVersionMax: 1);

            pruned.OrderBy(e => e).Should().BeEquivalentTo(expected.OrderBy(e => e));
        }

        [Fact]
        public void RetentionUtility_PruneWithMixedIdCasing()
        {
            var feed = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                new PackageIdentity("A", NuGetVersion.Parse("3.0.0")),
                new PackageIdentity("A", NuGetVersion.Parse("4.0.0")),
            };

            var pinned = new HashSet<PackageIdentity>();

            var expected = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                new PackageIdentity("A", NuGetVersion.Parse("3.0.0")),
            };

            var pruned = RetentionUtility.GetPackagesToPrune(feed, pinned, stableVersionMax: 1, prereleaseVersionMax: 1);

            pruned.OrderBy(e => e).Should().BeEquivalentTo(expected.OrderBy(e => e));
        }

        [Fact]
        public void RetentionUtility_PruneOnlyPrerelease()
        {
            var feed = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("3.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("4.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("5.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("5.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("6.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
            };

            var pinned = new HashSet<PackageIdentity>();

            var expected = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("3.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("4.0.0-beta")),
            };

            var pruned = RetentionUtility.GetPackagesToPrune(feed, pinned, stableVersionMax: 1024, prereleaseVersionMax: 1);

            pruned.OrderBy(e => e).Should().BeEquivalentTo(expected.OrderBy(e => e));
        }

        [Fact]
        public void RetentionUtility_PruneWithAllVersionsPinned()
        {
            var feed = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("3.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("3.0.0")),
            };

            var pinned = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("3.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("3.0.0")),
            };

            var expected = new HashSet<PackageIdentity>()
            {
            };

            var pruned = RetentionUtility.GetPackagesToPrune(feed, pinned, stableVersionMax: 1, prereleaseVersionMax: 1);

            pruned.OrderBy(e => e).Should().BeEquivalentTo(expected.OrderBy(e => e));
        }

        [Fact]
        public void RetentionUtility_PinnedVersionsDoNotCauseHigherVersionsToGetPruned()
        {
            var feed = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("3.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("4.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("5.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("6.0.0")),
            };

            var pinned = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("3.0.0")),
            };

            var expected = new HashSet<PackageIdentity>()
            {
            };

            var pruned = RetentionUtility.GetPackagesToPrune(feed, pinned, stableVersionMax: 3, prereleaseVersionMax: 1);

            pruned.OrderBy(e => e).Should().BeEquivalentTo(expected.OrderBy(e => e));
        }

        [Fact]
        public void RetentionUtility_PruneReleaseLabels_TwoLabels_Basic()
        {
            var feed = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.2")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.3")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.2")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.3")),
            };

            var pinned = new HashSet<PackageIdentity>();

            var expected = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.1")),
            };

            var pruned = RetentionUtility.GetPackagesToPrune(feed, pinned, stableVersionMax: 1024, prereleaseVersionMax: 2, groupByUniqueReleaseLabelCount: 2);

            pruned.OrderBy(e => e).Should().BeEquivalentTo(expected.OrderBy(e => e));
        }

        [Fact]
        public void RetentionUtility_PruneReleaseLabels_TwoLabels_MajorVersionDoesNotMatter()
        {
            var feed = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.1.1-beta.a.1")),
                new PackageIdentity("a", NuGetVersion.Parse("2.2.2-beta.a.2")),
                new PackageIdentity("a", NuGetVersion.Parse("3.3.3-beta.a.3")),
                new PackageIdentity("a", NuGetVersion.Parse("4.4.4-beta.b.1")),
                new PackageIdentity("a", NuGetVersion.Parse("5.5.5-beta.b.2")),
                new PackageIdentity("a", NuGetVersion.Parse("6.6.6-beta.b.3")),
            };

            var pinned = new HashSet<PackageIdentity>();

            var expected = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.1.1-beta.a.1")),
                new PackageIdentity("a", NuGetVersion.Parse("4.4.4-beta.b.1")),
            };

            var pruned = RetentionUtility.GetPackagesToPrune(feed, pinned, stableVersionMax: 1024, prereleaseVersionMax: 2, groupByUniqueReleaseLabelCount: 2);

            pruned.OrderBy(e => e).Should().BeEquivalentTo(expected.OrderBy(e => e));
        }

        [Fact]
        public void RetentionUtility_PruneReleaseLabels_TwoLabels_MajorVersionDoesNotMatter_2()
        {
            var feed = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.1.1-beta.a.1")),
                new PackageIdentity("a", NuGetVersion.Parse("2.2.2-beta.a.2")),
                new PackageIdentity("a", NuGetVersion.Parse("3.3.3-beta.a.3")),
                new PackageIdentity("a", NuGetVersion.Parse("3.3.3-beta.b.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.2.2-beta.b.2")),
                new PackageIdentity("a", NuGetVersion.Parse("1.1.1-beta.b.3")),
            };

            var pinned = new HashSet<PackageIdentity>();

            var expected = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.1.1-beta.a.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.1.1-beta.b.3")),
            };

            var pruned = RetentionUtility.GetPackagesToPrune(feed, pinned, stableVersionMax: 1024, prereleaseVersionMax: 2, groupByUniqueReleaseLabelCount: 2);

            pruned.OrderBy(e => e).Should().BeEquivalentTo(expected.OrderBy(e => e));
        }

        [Fact]
        public void RetentionUtility_PruneReleaseLabels_TwoLabels()
        {
            var feed = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.2")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.3")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.A.4")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.2")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.3")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.5"))
            };

            var pinned = new HashSet<PackageIdentity>();

            var expected = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.2")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.1")),
            };

            var pruned = RetentionUtility.GetPackagesToPrune(feed, pinned, stableVersionMax: 1024, prereleaseVersionMax: 2, groupByUniqueReleaseLabelCount: 2);

            pruned.OrderBy(e => e).Should().BeEquivalentTo(expected.OrderBy(e => e));
        }

        [Fact]
        public void RetentionUtility_PruneReleaseLabels_TwoLabels_VerifyPerId()
        {
            var feed = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("b", NuGetVersion.Parse("1.0.0-beta.a.1")),
                new PackageIdentity("b", NuGetVersion.Parse("1.0.0-beta.a.2")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.2")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.3")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.A.4")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.2")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.3")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.5"))
            };

            var pinned = new HashSet<PackageIdentity>();

            var expected = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.2")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.1")),
            };

            var pruned = RetentionUtility.GetPackagesToPrune(feed, pinned, stableVersionMax: 1024, prereleaseVersionMax: 2, groupByUniqueReleaseLabelCount: 2);

            pruned.OrderBy(e => e).Should().BeEquivalentTo(expected.OrderBy(e => e));
        }

        [Fact]
        public void RetentionUtility_PruneReleaseLabels_OneLabel()
        {
            var feed = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.2")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.3")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.4")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.2")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.3")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.5"))
            };

            var pinned = new HashSet<PackageIdentity>();

            var expected = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.2")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.3")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a.4")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.a")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.b.1")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta")),
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.5"))
            };

            var pruned = RetentionUtility.GetPackagesToPrune(feed, pinned, stableVersionMax: 1024, prereleaseVersionMax: 2, groupByUniqueReleaseLabelCount: 1);

            pruned.OrderBy(e => e).Should().BeEquivalentTo(expected.OrderBy(e => e));
        }

        [Theory]
        [InlineData("1.0.0-beta.a.1+xyz", 0, "")]
        [InlineData("1.0.0-beta.a.1+xyz", 1, "beta")]
        [InlineData("1.0.0-beta.a.1+xyz", 2, "beta.a")]
        [InlineData("1.0.0-beta.a.1+xyz", -1, "")]
        [InlineData("1.0.0-beta.a.1+xyz", 5, "beta.a.1")]
        public void RetentionUtility_GetReleaseLabelKey_NullParts(string version, int count, string expected)
        {
            RetentionUtility.GetReleaseLabelKey(NuGetVersion.Parse(version), count).Should().Be(expected);
        }
    }
}