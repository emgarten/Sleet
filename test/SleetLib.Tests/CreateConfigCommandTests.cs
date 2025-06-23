using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class CreateConfigCommandTests
    {
        [Fact]
        public async Task CreateConfigCommand_WithLocalStorageType_CreatesValidConfig()
        {
            using (var testDir = new TestFolder())
            {
                var configPath = Path.Combine(testDir.Root, "sleet.json");
                var result = await CreateConfigCommand.RunAsync(FileSystemStorageType.Local, testDir.Root, NullLogger.Instance);

                result.Should().BeTrue();
                File.Exists(configPath).Should().BeTrue();

                var json = JObject.Parse(File.ReadAllText(configPath));
                json["username"].Value<string>().Should().Be("");
                json["useremail"].Value<string>().Should().Be("");
                json["sources"].Should().NotBeNull();
                json["sources"].Count().Should().Be(1);

                var source = json["sources"][0];
                source["name"].Value<string>().Should().Be("myLocalFeed");
                source["type"].Value<string>().Should().Be("local");
                source["path"].Value<string>().Should().NotBeNullOrEmpty();
                source["baseURI"].Value<string>().Should().Be("https://example.com/feed/");
            }
        }

        [Fact]
        public async Task CreateConfigCommand_WithAzureStorageType_CreatesValidConfig()
        {
            using (var testDir = new TestFolder())
            {
                var configPath = Path.Combine(testDir.Root, "sleet.json");
                var result = await CreateConfigCommand.RunAsync(FileSystemStorageType.Azure, testDir.Root, NullLogger.Instance);

                result.Should().BeTrue();
                File.Exists(configPath).Should().BeTrue();

                var json = JObject.Parse(File.ReadAllText(configPath));
                var source = json["sources"][0];
                source["name"].Value<string>().Should().Be("myAzureFeed");
                source["type"].Value<string>().Should().Be("azure");
                source["container"].Value<string>().Should().Be("myfeed");
                source["connectionString"].Value<string>().Should().Be(AzureFileSystem.AzureEmptyConnectionString);
            }
        }

        [Fact]
        public async Task CreateConfigCommand_WithS3StorageType_CreatesValidConfig()
        {
            using (var testDir = new TestFolder())
            {
                var configPath = Path.Combine(testDir.Root, "sleet.json");
                var result = await CreateConfigCommand.RunAsync(FileSystemStorageType.S3, testDir.Root, NullLogger.Instance);

                result.Should().BeTrue();
                File.Exists(configPath).Should().BeTrue();

                var json = JObject.Parse(File.ReadAllText(configPath));
                var source = json["sources"][0];
                source["name"].Value<string>().Should().Be("myAmazonS3Feed");
                source["type"].Value<string>().Should().Be("s3");
                source["bucketName"].Value<string>().Should().Be("bucketname");
                source["region"].Value<string>().Should().Be("us-east-1");
                source["profileName"].Value<string>().Should().Be("credentialsFileProfileName");
            }
        }

        [Fact]
        public async Task CreateConfigCommand_WithUnspecifiedStorageType_CreatesValidConfig()
        {
            using (var testDir = new TestFolder())
            {
                var configPath = Path.Combine(testDir.Root, "sleet.json");
                var result = await CreateConfigCommand.RunAsync(FileSystemStorageType.Unspecified, testDir.Root, NullLogger.Instance);

                result.Should().BeTrue();
                File.Exists(configPath).Should().BeTrue();

                var json = JObject.Parse(File.ReadAllText(configPath));
                var source = json["sources"][0];
                source["name"].Value<string>().Should().Be("myFeed");
                source["type"].Value<string>().Should().Be("");
            }
        }

        [Fact]
        public async Task CreateConfigCommand_WithSpecificFileName_CreatesConfigAtPath()
        {
            using (var testDir = new TestFolder())
            {
                var configPath = Path.Combine(testDir.Root, "custom-config.json");
                var result = await CreateConfigCommand.RunAsync(FileSystemStorageType.Local, configPath, NullLogger.Instance);

                result.Should().BeTrue();
                File.Exists(configPath).Should().BeTrue();
            }
        }

        [Fact]
        public async Task CreateConfigCommand_WithExistingFile_ReturnsFalse()
        {
            using (var testDir = new TestFolder())
            {
                var configPath = Path.Combine(testDir.Root, "sleet.json");
                File.WriteAllText(configPath, "existing content");

                var result = await CreateConfigCommand.RunAsync(FileSystemStorageType.Local, testDir.Root, NullLogger.Instance);

                result.Should().BeFalse();
            }
        }

        [Fact]
        public async Task CreateConfigCommand_WithNonExistentDirectory_ReturnsFalse()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "config.json");
            
            var result = await CreateConfigCommand.RunAsync(FileSystemStorageType.Local, nonExistentPath, NullLogger.Instance);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task CreateConfigCommand_WithNullOutput_UsesCurrentDirectory()
        {
            using (var testDir = new TestFolder())
            {
                var originalDir = Directory.GetCurrentDirectory();
                try
                {
                    Directory.SetCurrentDirectory(testDir.Root);
                    var result = await CreateConfigCommand.RunAsync(FileSystemStorageType.Local, null, NullLogger.Instance);

                    result.Should().BeTrue();
                    File.Exists(Path.Combine(testDir.Root, "sleet.json")).Should().BeTrue();
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDir);
                }
            }
        }

        [Fact]
        public async Task CreateConfigCommand_WithEmptyOutput_UsesCurrentDirectory()
        {
            using (var testDir = new TestFolder())
            {
                var originalDir = Directory.GetCurrentDirectory();
                try
                {
                    Directory.SetCurrentDirectory(testDir.Root);
                    var result = await CreateConfigCommand.RunAsync(FileSystemStorageType.Local, string.Empty, NullLogger.Instance);

                    result.Should().BeTrue();
                    File.Exists(Path.Combine(testDir.Root, "sleet.json")).Should().BeTrue();
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDir);
                }
            }
        }
    }
}