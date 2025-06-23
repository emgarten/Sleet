using System;
using FluentAssertions;
using NuGet.Versioning;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class FeedCapabilityTests
    {
        [Fact]
        public void FeedCapability_ToString_ReturnsCorrectFormat()
        {
            var capability = new FeedCapability
            {
                Name = "TestFeature",
                Version = new SemanticVersion(1, 2, 3)
            };

            var result = capability.ToString();

            result.Should().Be("testfeature:1.2.3");
        }

        [Fact]
        public void FeedCapability_ToString_WithPrerelease_ReturnsCorrectFormat()
        {
            var capability = new FeedCapability
            {
                Name = "TestFeature",
                Version = new SemanticVersion(1, 2, 3, "beta")
            };

            var result = capability.ToString();

            result.Should().Be("testfeature:1.2.3-beta");
        }

        [Fact]
        public void FeedCapability_Parse_WithVersionString_ReturnsCorrectCapability()
        {
            var result = FeedCapability.Parse("TestFeature:1.2.3");

            result.Name.Should().Be("testfeature");
            result.Version.Should().Be(new SemanticVersion(1, 2, 3));
        }

        [Fact]
        public void FeedCapability_Parse_WithPrerelease_ReturnsCorrectCapability()
        {
            var result = FeedCapability.Parse("TestFeature:1.2.3-beta");

            result.Name.Should().Be("testfeature");
            result.Version.Should().Be(new SemanticVersion(1, 2, 3, "beta"));
        }

        [Fact]
        public void FeedCapability_Parse_WithoutVersion_ThrowsIndexOutOfRangeException()
        {
            Action act = () => FeedCapability.Parse("TestFeature");

            Assert.Throws<IndexOutOfRangeException>(act);
        }

        [Fact]
        public void FeedCapability_Parse_WithMixedCase_NormalizesToLowercase()
        {
            var result = FeedCapability.Parse("TestFeature:1.2.3");

            result.Name.Should().Be("testfeature");
            result.ToString().Should().Be("testfeature:1.2.3");
        }

        [Fact]
        public void FeedCapability_Parse_WithEmptyString_ThrowsIndexOutOfRangeException()
        {
            Action act = () => FeedCapability.Parse("");

            Assert.Throws<IndexOutOfRangeException>(act);
        }

        [Fact]
        public void FeedCapability_Parse_WithNullString_ThrowsNullReferenceException()
        {
            Action act = () => FeedCapability.Parse(null);

            Assert.Throws<NullReferenceException>(act);
        }

        [Fact]
        public void FeedCapability_Parse_WithInvalidVersion_ThrowsArgumentException()
        {
            Action act = () => FeedCapability.Parse("TestFeature:invalid.version");

            Assert.Throws<ArgumentException>(act);
        }

        [Fact]
        public void FeedCapability_Parse_WithColonButNoVersion_ThrowsArgumentException()
        {
            Action act = () => FeedCapability.Parse("TestFeature:");

            Assert.Throws<ArgumentException>(act);
        }

        [Fact]
        public void FeedCapability_Parse_WithMultipleColons_ParsesFirstTwoSegments()
        {
            var result = FeedCapability.Parse("TestFeature:1.2.3:Extra");

            result.Name.Should().Be("testfeature");
            result.Version.Should().Be(new SemanticVersion(1, 2, 3));
        }

        [Theory]
        [InlineData("MyFeature", "1.0.0", "myfeature:1.0.0")]
        [InlineData("UPPERCASE", "2.1.0", "uppercase:2.1.0")]
        [InlineData("mixed-Case_123", "1.2.3-alpha", "mixed-case_123:1.2.3-alpha")]
        public void FeedCapability_ToString_VariousInputs_ReturnsExpectedFormat(string name, string version, string expected)
        {
            var capability = new FeedCapability
            {
                Name = name,
                Version = SemanticVersion.Parse(version)
            };

            capability.ToString().Should().Be(expected);
        }

        [Theory]
        [InlineData("MyFeature:1.0.0")]
        [InlineData("UPPERCASE:2.1.0")]
        [InlineData("mixed-Case_123:1.2.3-alpha")]
        public void FeedCapability_ParseAndToString_RoundTrip_IsConsistent(string input)
        {
            var capability = FeedCapability.Parse(input);
            var reparsed = FeedCapability.Parse(capability.ToString());

            reparsed.Name.Should().Be(capability.Name);
            reparsed.Version.Should().Be(capability.Version);
        }
    }
}