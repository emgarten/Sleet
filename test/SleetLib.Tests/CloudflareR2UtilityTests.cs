using System;
using FluentAssertions;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class CloudflareR2UtilityTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("default")]
        [InlineData("Default")]
        public void GetServiceUrl_WithDefaultJurisdiction_ReturnsAccountEndpoint(string jurisdiction)
        {
            var result = CloudflareR2Utility.GetServiceUrl("abc123", jurisdiction);

            result.Should().Be("https://abc123.r2.cloudflarestorage.com");
        }

        [Theory]
        [InlineData("eu", "https://abc123.eu.r2.cloudflarestorage.com")]
        [InlineData("us", "https://abc123.us.r2.cloudflarestorage.com")]
        [InlineData("FedRAMP", "https://abc123.fedramp.r2.cloudflarestorage.com")]
        public void GetServiceUrl_WithJurisdiction_ReturnsJurisdictionEndpoint(string jurisdiction, string expected)
        {
            var result = CloudflareR2Utility.GetServiceUrl("abc123", jurisdiction);

            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetServiceUrl_WithoutAccountId_ThrowsArgumentException(string accountId)
        {
            Action act = () => CloudflareR2Utility.GetServiceUrl(accountId, null);

            var ex = Assert.Throws<ArgumentException>(act);
            Assert.Contains("Missing accountId for Cloudflare R2 account.", ex.Message);
        }

        [Fact]
        public void GetServiceUrl_WithInvalidJurisdiction_ThrowsArgumentException()
        {
            Action act = () => CloudflareR2Utility.GetServiceUrl("abc123", "mars");

            var ex = Assert.Throws<ArgumentException>(act);
            Assert.Contains("Invalid jurisdiction 'mars' for Cloudflare R2 account.", ex.Message);
        }

        [Fact]
        public void GetPublicAccessHelpMessage_ContainsBucketName()
        {
            var result = CloudflareR2Utility.GetPublicAccessHelpMessage("myfeed");

            result.Should().Contain("myfeed");
            result.Should().Contain("https://developers.cloudflare.com/r2/buckets/public-buckets/");
        }
    }
}
