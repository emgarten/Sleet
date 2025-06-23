using System.Linq;
using FluentAssertions;
using NuGet.Versioning;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class FeedRequirementsTests
    {
        [Fact]
        public void FeedRequirements_DefaultConstructor_SetsDefaultValues()
        {
            var requirements = new FeedRequirements();

            requirements.CreatorSleetVersion.Should().Be(new SemanticVersion(1, 0, 0));
            requirements.RequiredVersion.Should().Be(VersionRange.All);
            requirements.RequiredCapabilities.Should().NotBeNull();
            requirements.RequiredCapabilities.Should().BeEmpty();
        }

        [Fact]
        public void FeedRequirements_CreatorSleetVersion_CanBeSet()
        {
            var requirements = new FeedRequirements();
            var version = new SemanticVersion(2, 1, 0);

            requirements.CreatorSleetVersion = version;

            requirements.CreatorSleetVersion.Should().Be(version);
        }

        [Fact]
        public void FeedRequirements_RequiredVersion_CanBeSet()
        {
            var requirements = new FeedRequirements();
            var versionRange = VersionRange.Parse("[2.0.0,)");

            requirements.RequiredVersion = versionRange;

            requirements.RequiredVersion.Should().Be(versionRange);
        }

        [Fact]
        public void FeedRequirements_RequiredCapabilities_CanBeModified()
        {
            var requirements = new FeedRequirements();
            var capability = new FeedCapability
            {
                Name = "TestFeature",
                Version = new SemanticVersion(1, 0, 0)
            };

            requirements.RequiredCapabilities.Add(capability);

            requirements.RequiredCapabilities.Should().HaveCount(1);
            requirements.RequiredCapabilities.First().Should().Be(capability);
        }

        [Fact]
        public void FeedRequirements_RequiredCapabilities_CanBeReplaced()
        {
            var requirements = new FeedRequirements();
            var capabilities = new[]
            {
                new FeedCapability { Name = "Feature1", Version = new SemanticVersion(1, 0, 0) },
                new FeedCapability { Name = "Feature2", Version = new SemanticVersion(2, 0, 0) }
            };

            requirements.RequiredCapabilities = capabilities.ToList();

            requirements.RequiredCapabilities.Should().HaveCount(2);
            requirements.RequiredCapabilities[0].Name.Should().Be("Feature1");
            requirements.RequiredCapabilities[1].Name.Should().Be("Feature2");
        }

        [Fact]
        public void FeedRequirements_AllProperties_CanBeSetIndependently()
        {
            var requirements = new FeedRequirements();
            var creatorVersion = new SemanticVersion(3, 2, 1);
            var requiredVersion = VersionRange.Parse("[3.0.0,4.0.0)");
            var capability = new FeedCapability { Name = "AdvancedFeature", Version = new SemanticVersion(1, 5, 0) };

            requirements.CreatorSleetVersion = creatorVersion;
            requirements.RequiredVersion = requiredVersion;
            requirements.RequiredCapabilities.Add(capability);

            requirements.CreatorSleetVersion.Should().Be(creatorVersion);
            requirements.RequiredVersion.Should().Be(requiredVersion);
            requirements.RequiredCapabilities.Should().Contain(capability);
        }
    }
}