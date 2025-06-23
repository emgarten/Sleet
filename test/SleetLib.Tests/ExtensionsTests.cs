using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void GetDateString_ReturnsISOFormat()
        {
            var date = new DateTimeOffset(2023, 10, 15, 14, 30, 45, TimeSpan.Zero);

            var result = date.GetDateString();

            result.Should().Be("2023-10-15T14:30:45.0000000Z");
        }

        [Fact]
        public void GetDateString_WithDifferentTimezone_ConvertsToUTC()
        {
            var date = new DateTimeOffset(2023, 10, 15, 14, 30, 45, TimeSpan.FromHours(5));

            var result = date.GetDateString();

            result.Should().Be("2023-10-15T09:30:45.0000000Z");
        }

        [Fact]
        public void ToIdentityString_WithReleaseVersion_ReturnsVersionOnly()
        {
            var version = new SemanticVersion(1, 2, 3);

            var result = version.ToIdentityString();

            result.Should().Be("1.2.3");
        }

        [Fact]
        public void ToIdentityString_WithPrereleaseVersion_IncludesPrerelease()
        {
            var version = new SemanticVersion(1, 2, 3, "beta");

            var result = version.ToIdentityString();

            result.Should().Be("1.2.3-beta");
        }

        [Fact]
        public void ToFullVersionString_WithReleaseVersion_ReturnsVersionOnly()
        {
            var version = new SemanticVersion(1, 2, 3);

            var result = version.ToFullVersionString();

            result.Should().Be("1.2.3");
        }

        [Fact]
        public void ToFullVersionString_WithPrereleaseVersion_IncludesPrerelease()
        {
            var version = new SemanticVersion(1, 2, 3, "beta");

            var result = version.ToFullVersionString();

            result.Should().Be("1.2.3-beta");
        }

        [Fact]
        public void GetVersion_WithValidVersionProperty_ReturnsNuGetVersion()
        {
            var json = new JObject
            {
                ["version"] = "1.2.3-beta"
            };

            var result = json.GetVersion();

            result.Should().Be(NuGetVersion.Parse("1.2.3-beta"));
        }

        [Fact]
        public void GetId_WithValidIdProperty_ReturnsId()
        {
            var json = new JObject
            {
                ["id"] = "TestPackage"
            };

            var result = json.GetId();

            result.Should().Be("TestPackage");
        }

        [Fact]
        public void GetIdentity_WithValidIdAndVersion_ReturnsPackageIdentity()
        {
            var json = new JObject
            {
                ["id"] = "TestPackage",
                ["version"] = "1.2.3"
            };

            var result = json.GetIdentity();

            result.Id.Should().Be("TestPackage");
            result.Version.Should().Be(NuGetVersion.Parse("1.2.3"));
        }

        [Fact]
        public void GetEntityId_WithValidAtIdProperty_ReturnsUri()
        {
            var json = new JObject
            {
                ["@id"] = "https://example.com/test"
            };

            var result = json.GetEntityId();

            result.Should().Be(new Uri("https://example.com/test"));
        }

        [Fact]
        public void GetString_WithValidProperty_ReturnsString()
        {
            var json = new JObject
            {
                ["testProperty"] = "testValue"
            };

            var result = json.GetString("testProperty");

            result.Should().Be("testValue");
        }

        [Fact]
        public void GetString_WithNonExistentProperty_ReturnsNull()
        {
            var json = new JObject();

            var result = json.GetString("nonExistent");

            result.Should().BeNull();
        }

        [Fact]
        public void GetJObjectArray_WithValidArray_ReturnsJObjectArray()
        {
            var json = new JObject
            {
                ["testArray"] = new JArray
                {
                    new JObject { ["item1"] = "value1" },
                    new JObject { ["item2"] = "value2" }
                }
            };

            var result = json.GetJObjectArray("testArray");

            result.Should().HaveCount(2);
            result[0]["item1"].Value<string>().Should().Be("value1");
            result[1]["item2"].Value<string>().Should().Be("value2");
        }

        [Fact]
        public void GetJObjectArray_WithNonExistentProperty_ReturnsEmptyArray()
        {
            var json = new JObject();

            var result = json.GetJObjectArray("nonExistent");

            result.Should().BeEmpty();
        }

        [Fact]
        public void GetJObjectArray_WithNonArrayProperty_ReturnsEmptyArray()
        {
            var json = new JObject
            {
                ["testProperty"] = "notAnArray"
            };

            var result = json.GetJObjectArray("testProperty");

            result.Should().BeEmpty();
        }

        [Fact]
        public void AsMemoryStream_WithValidXDocument_ReturnsMemoryStream()
        {
            var doc = new XDocument(new XElement("root", new XElement("child", "content")));

            var result = doc.AsMemoryStream();

            result.Should().NotBeNull();
            result.Position.Should().Be(0);
            result.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task AsMemoryStreamAsync_WithValidStream_ReturnsMemoryStream()
        {
            var originalContent = "Test content";
            var originalStream = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));

            var result = await originalStream.AsMemoryStreamAsync();

            result.Should().NotBeNull();
            result.Position.Should().Be(0);
            var content = Encoding.UTF8.GetString(result.ToArray());
            content.Should().Be(originalContent);
        }

        [Fact]
        public async Task AsMemoryStreamAsync_WithNullStream_ThrowsArgumentNullException()
        {
            Stream nullStream = null;

            Func<Task> act = async () => await nullStream.AsMemoryStreamAsync();

            await Assert.ThrowsAsync<ArgumentNullException>(act);
        }

        [Fact]
        public async Task AsMemoryStreamAsync_WithSeekableStream_ResetsPosition()
        {
            var originalContent = "Test content";
            var originalStream = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
            originalStream.Position = 5; // Move position to middle

            var result = await originalStream.AsMemoryStreamAsync();

            var content = Encoding.UTF8.GetString(result.ToArray());
            content.Should().Be(originalContent); // Should get full content, not from position 5
        }

    }
}