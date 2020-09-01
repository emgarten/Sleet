using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class BadgeUtilityTests
    {
        [Fact]
        public void BadgeUtility_GetChanges_EmptySets()
        {
            var before = new HashSet<PackageIdentity>();
            var after = new HashSet<PackageIdentity>();


            BadgeUtility.GetChanges(before, after, preRel: false).Should().BeEmpty();
        }

        [Fact]
        public void BadgeUtility_GetChanges_NewPackage()
        {
            var before = new HashSet<PackageIdentity>();
            var after = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0"))
            };

            BadgeUtility.GetChanges(before, after, preRel: false).Count.Should().Be(1);
        }

        [Fact]
        public void BadgeUtility_GetChanges_NoPackageChanges()
        {
            var before = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("A", NuGetVersion.Parse("1.0.0-a"))
            };
            var after = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-a"))
            };

            BadgeUtility.GetChanges(before, after, preRel: true).Should().BeEmpty();
        }

        [Fact]
        public void BadgeUtility_GetChanges_RemovedPackage()
        {
            var before = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0"))
            };
            var after = new HashSet<PackageIdentity>()
            {
            };

            BadgeUtility.GetChanges(before, after, preRel: false).Count.Should().Be(1);
        }

        [Fact]
        public void BadgeUtility_GetChanges_MultipleAddedInSameId()
        {
            var before = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0"))
            };
            var after = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("3.0.0"))
            };

            BadgeUtility.GetChanges(before, after, preRel: false).Count.Should().Be(1);
            BadgeUtility.GetChanges(before, after, preRel: false).First().Should().Be(new PackageIdentity("a", NuGetVersion.Parse("3.0.0")));
        }

        [Fact]
        public void BadgeUtility_GetChanges_MultipleAddedInSameId_NoStable()
        {
            var before = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0-a"))
            };
            var after = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0-a")),
                new PackageIdentity("a", NuGetVersion.Parse("3.0.0-a"))
            };

            BadgeUtility.GetChanges(before, after, preRel: false).Count.Should().Be(1);
            BadgeUtility.GetChanges(before, after, preRel: false).First().Should().Be(new PackageIdentity("a", NuGetVersion.Parse("3.0.0-a")));
        }

        [Fact]
        public void BadgeUtility_GetChanges_MultipleAddedInSameId_WithStable()
        {
            var before = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0"))
            };
            var after = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                new PackageIdentity("a", NuGetVersion.Parse("3.0.0-a"))
            };

            BadgeUtility.GetChanges(before, after, preRel: false).Count.Should().Be(1);
            BadgeUtility.GetChanges(before, after, preRel: false).First().Should().Be(new PackageIdentity("a", NuGetVersion.Parse("2.0.0")));

            BadgeUtility.GetChanges(before, after, preRel: true).Count.Should().Be(1);
            BadgeUtility.GetChanges(before, after, preRel: true).First().Should().Be(new PackageIdentity("a", NuGetVersion.Parse("3.0.0-a")));
        }

        [Fact]
        public void BadgeUtility_GetChanges_MultipleIds()
        {
            var before = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0"))
            };
            var after = new HashSet<PackageIdentity>()
            {
                new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                new PackageIdentity("b", NuGetVersion.Parse("1.0.0"))
            };

            BadgeUtility.GetChanges(before, after, preRel: false).Count.Should().Be(1);
            BadgeUtility.GetChanges(before, after, preRel: false).First().Should().Be(new PackageIdentity("b", NuGetVersion.Parse("1.0.0")));
        }
    }
}
