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
    }
}