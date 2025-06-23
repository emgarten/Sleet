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
    public class FileSystemFactoryTests
    {
        [Fact]
        public async Task CreateFileSystemAsync_WithNoSources_ThrowsArgumentException()
        {
            var settings = new LocalSettings();
            settings.Json = new JObject();
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "test", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Invalid config. No sources found.", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithEmptySourcesArray_ReturnsNull()
        {
            var settings = new LocalSettings();
            settings.Json = new JObject
            {
                ["sources"] = new JArray()
            };
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "nonexistent", NullLogger.Instance);

            result.Should().BeNull();
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithNonMatchingSourceName_ReturnsNull()
        {
            var settings = new LocalSettings();
            settings.Json = new JObject
            {
                ["sources"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "differentName",
                        ["type"] = "local",
                        ["path"] = "/tmp/test"
                    }
                }
            };
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "targetName", NullLogger.Instance);

            result.Should().BeNull();
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithLocalType_WithoutPath_ThrowsArgumentException()
        {
            var settings = new LocalSettings();
            settings.Json = new JObject
            {
                ["sources"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "local",
                        ["type"] = "local"
                    }
                }
            };
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "local", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Missing path for account.", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithLocalType_WithRelativePathAndNoSettings_ThrowsArgumentException()
        {
            var settings = new LocalSettings();
            settings.Json = new JObject
            {
                ["sources"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "local",
                        ["type"] = "local",
                        ["path"] = "relative/path"
                    }
                }
            };
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "local", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Cannot use a relative 'path' without a sleet.json file.", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithAzureType_WithoutConnectionString_ThrowsArgumentException()
        {
            var settings = new LocalSettings();
            settings.Json = new JObject
            {
                ["sources"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "azure",
                        ["type"] = "azure",
                        ["container"] = "test"
                    }
                }
            };
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "azure", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Missing connectionString for azure account.", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithAzureType_WithoutContainer_ThrowsArgumentException()
        {
            var settings = new LocalSettings();
            settings.Json = new JObject
            {
                ["sources"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "azure",
                        ["type"] = "azure",
                        ["connectionString"] = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key;"
                    }
                }
            };
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "azure", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Missing container for azure account.", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithAzureType_WithEmptyConnectionString_ThrowsArgumentException()
        {
            var settings = new LocalSettings();
            settings.Json = new JObject
            {
                ["sources"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "azure",
                        ["type"] = "azure",
                        ["connectionString"] = AzureFileSystem.AzureEmptyConnectionString,
                        ["container"] = "test"
                    }
                }
            };
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "azure", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Invalid connectionString for azure account.", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithoutBucketName_ThrowsArgumentException()
        {
            var settings = new LocalSettings();
            settings.Json = new JObject
            {
                ["sources"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "s3",
                        ["type"] = "s3",
                        ["region"] = "us-east-1"
                    }
                }
            };
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Missing bucketName for Amazon S3 account.", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithoutRegionOrServiceURL_ThrowsArgumentException()
        {
            var settings = new LocalSettings();
            settings.Json = new JObject
            {
                ["sources"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "s3",
                        ["type"] = "s3",
                        ["bucketName"] = "test-bucket"
                    }
                }
            };
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Either 'region' or 'serviceURL' must be specified for an Amazon S3 account", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithBothRegionAndServiceURL_ThrowsArgumentException()
        {
            var settings = new LocalSettings();
            settings.Json = new JObject
            {
                ["sources"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "s3",
                        ["type"] = "s3",
                        ["bucketName"] = "test-bucket",
                        ["region"] = "us-east-1",
                        ["serviceURL"] = "https://s3.example.com"
                    }
                }
            };
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Options 'region' and 'serviceURL' cannot be used together", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithInvalidServerSideEncryption_ThrowsArgumentException()
        {
            var settings = new LocalSettings();
            settings.Json = new JObject
            {
                ["sources"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "s3",
                        ["type"] = "s3",
                        ["bucketName"] = "test-bucket",
                        ["region"] = "us-east-1",
                        ["serverSideEncryptionMethod"] = "InvalidMethod"
                    }
                }
            };
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Only 'None' or 'AES256' are currently supported for serverSideEncryptionMethod", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithInvalidProfileName_ThrowsArgumentException()
        {
            var settings = new LocalSettings();
            settings.Json = new JObject
            {
                ["sources"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "s3",
                        ["type"] = "s3",
                        ["bucketName"] = "test-bucket",
                        ["region"] = "us-east-1",
                        ["profileName"] = "nonexistent-profile"
                    }
                }
            };
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("The specified AWS profileName nonexistent-profile could not be found", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithValidLocalConfiguration_CreatesPhysicalFileSystem()
        {
            using (var testDir = new TestFolder())
            {
                var settings = new LocalSettings();
                settings.Path = Path.Combine(testDir.Root, "sleet.json");
                settings.Json = new JObject
                {
                    ["sources"] = new JArray
                    {
                        new JObject
                        {
                            ["name"] = "local",
                            ["type"] = "local",
                            ["path"] = testDir.Root,
                            ["baseURI"] = "https://example.com/feed/"
                        }
                    }
                };
                var cache = new LocalCache();

                var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "local", NullLogger.Instance);

                result.Should().NotBeNull();
                result.Should().BeOfType<PhysicalFileSystem>();
            }
        }
    }
}