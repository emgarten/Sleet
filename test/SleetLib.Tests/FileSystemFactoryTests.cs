using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using AwesomeAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    [Collection(EnvironmentVariableCollection.Name)]
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
        public async Task CreateFileSystemAsync_WithLegacyMisspelledEnvFeedName_ResolvesRenamedFeed()
        {
            var settings = new LocalSettings();
            settings.Json = new JObject
            {
                ["sources"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "environment_feed",
                        ["type"] = "local",
                        ["path"] = "/tmp/test"
                    }
                }
            };
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "envirnoment_feed", NullLogger.Instance);

            result.Should().NotBeNull();
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

        [Fact]
        public async Task CreateFileSystemAsync_WithLocalType_WithEmptyPath_UsesSettingsDirectory()
        {
            using (var testDir = new TestFolder())
            {
                var settings = CreateSettings("local", new JObject
                {
                    ["type"] = "local",
                    ["path"] = string.Empty,
                    ["baseURI"] = "https://example.test/feed/"
                });
                settings.Path = Path.Combine(testDir.Root, "sleet.json");
                var cache = new LocalCache();

                var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "local", NullLogger.Instance);

                var fileSystem = Assert.IsType<PhysicalFileSystem>(result);
                fileSystem.Root.LocalPath.TrimEnd(Path.DirectorySeparatorChar).Should().Be(testDir.Root.TrimEnd(Path.DirectorySeparatorChar));
                fileSystem.BaseURI.AbsoluteUri.Should().Be("https://example.test/feed/");
            }
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithLocalType_WithRelativePath_ResolvesAgainstSettingsDirectory()
        {
            using (var testDir = new TestFolder())
            {
                var feedPath = Path.Combine(testDir.Root, "relative-feed");
                var settings = CreateSettings("local", new JObject
                {
                    ["type"] = "local",
                    ["path"] = "relative-feed"
                });
                settings.Path = Path.Combine(testDir.Root, "sleet.json");
                var cache = new LocalCache();

                var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "local", NullLogger.Instance);

                var fileSystem = Assert.IsType<PhysicalFileSystem>(result);
                fileSystem.Root.LocalPath.TrimEnd(Path.DirectorySeparatorChar).Should().Be(feedPath.TrimEnd(Path.DirectorySeparatorChar));
                fileSystem.BaseURI.Should().Be(fileSystem.Root);
            }
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithAzureType_WithPathOnly_CreatesAzureFileSystem()
        {
            var settings = CreateSettings("azure", new JObject
            {
                ["type"] = "azure",
                ["container"] = "feed",
                ["path"] = "https://account.blob.core.windows.net/feed/sub/",
                ["baseURI"] = "https://cdn.example.test/feed/sub/",
                ["feedSubPath"] = "ignored"
            });
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "azure", NullLogger.Instance);

            var fileSystem = Assert.IsType<AzureFileSystem>(result);
            fileSystem.Root.AbsoluteUri.Should().Be("https://account.blob.core.windows.net/feed/sub/");
            fileSystem.BaseURI.AbsoluteUri.Should().Be("https://cdn.example.test/feed/sub/");
            fileSystem.FeedSubPath.Should().Be("sub/");
            fileSystem.GetRelativePath(fileSystem.GetPath("index.json")).Should().Be("sub/index.json");
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithAzureType_WithConnectionString_CreatesAzureFileSystemAndLogsWarning()
        {
            var log = new TestLogger();
            var settings = CreateSettings("azure", new JObject
            {
                ["type"] = "azure",
                ["container"] = "feed",
                ["connectionString"] = GetAzureConnectionString(),
                ["immutableCacheControl"] = "public, max-age=31536000",
                ["mutableCacheControl"] = "no-cache"
            });
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "azure", log);

            var fileSystem = Assert.IsType<AzureFileSystem>(result);
            fileSystem.Root.AbsoluteUri.Should().Be("https://account.blob.core.windows.net/feed/");
            fileSystem.BaseURI.Should().Be(fileSystem.Root);
            log.GetMessages().Should().Contain("connectionString (with access key) is not recommended for azure account.");
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithExplicitKeysAndRegion_CreatesAmazonS3FileSystem()
        {
            var settings = CreateSettings("s3", new JObject
            {
                ["type"] = "s3",
                ["bucketName"] = "test-bucket",
                ["region"] = "us-east-1",
                ["accessKeyId"] = "access-key",
                ["secretAccessKey"] = "secret-key"
            });
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var fileSystem = Assert.IsType<AmazonS3FileSystem>(result);
            fileSystem.Root.AbsoluteUri.Should().Be("https://s3.amazonaws.com/test-bucket/");
            fileSystem.BaseURI.Should().Be(fileSystem.Root);
            fileSystem.FeedSubPath.Should().BeNull();
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithPathBaseUriAndOptions_CreatesAmazonS3FileSystem()
        {
            var settings = CreateSettings("s3", new JObject
            {
                ["type"] = "s3",
                ["bucketName"] = "bucket-name",
                ["region"] = "us-west-2",
                ["accessKeyId"] = "access-key",
                ["secretAccessKey"] = "secret-key",
                ["path"] = "https://s3-us-west-2.amazonaws.com/bucket-name/feed/sub/",
                ["baseURI"] = "https://cdn.example.test/feed/sub/",
                ["feedSubPath"] = "feed/sub",
                ["serverSideEncryptionMethod"] = "AES256",
                ["compress"] = false,
                ["acl"] = "public-read",
                ["disablePayloadSigning"] = true,
                ["immutableCacheControl"] = "public, max-age=31536000",
                ["mutableCacheControl"] = "no-cache"
            });
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var fileSystem = Assert.IsType<AmazonS3FileSystem>(result);
            fileSystem.Root.AbsoluteUri.Should().Be("https://s3-us-west-2.amazonaws.com/bucket-name/feed/sub/");
            fileSystem.BaseURI.AbsoluteUri.Should().Be("https://cdn.example.test/feed/sub/");
            fileSystem.FeedSubPath.Should().Be("feed/sub/");
            fileSystem.GetRelativePath(fileSystem.GetPath("index.json")).Should().Be("feed/sub/index.json");
            fileSystem.Get("index.json").EntityUri.AbsoluteUri.Should().Be("https://cdn.example.test/feed/sub/index.json");
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithServiceUrlAndPath_CreatesAmazonS3FileSystem()
        {
            var settings = CreateSettings("s3", new JObject
            {
                ["type"] = "s3",
                ["bucketName"] = "bucket-name",
                ["serviceURL"] = "https://s3.example.test",
                ["accessKeyId"] = "access-key",
                ["secretAccessKey"] = "secret-key",
                ["path"] = "https://s3.example.test/bucket-name/"
            });
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var fileSystem = Assert.IsType<AmazonS3FileSystem>(result);
            fileSystem.Root.AbsoluteUri.Should().Be("https://s3.example.test/bucket-name/");
            fileSystem.BaseURI.Should().Be(fileSystem.Root);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithEnvironmentCredentials_CreatesAmazonS3FileSystem()
        {
            using (new EnvironmentVariableScope(
                ("AWS_ACCESS_KEY_ID", "environment-access-key"),
                ("AWS_SECRET_ACCESS_KEY", "environment-secret-key"),
                ("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", null)))
            {
                var settings = CreateSettings("s3", new JObject
                {
                    ["type"] = "s3",
                    ["bucketName"] = "test-bucket",
                    ["region"] = "us-east-2"
                });
                var cache = new LocalCache();

                var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

                var fileSystem = Assert.IsType<AmazonS3FileSystem>(result);
                fileSystem.Root.AbsoluteUri.Should().Be("https://s3-us-east-2.amazonaws.com/test-bucket/");
            }
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithCredentialsProfile_CreatesAmazonS3FileSystem()
        {
            using (var testDir = new TestFolder())
            {
                var credentialsFile = Path.Combine(testDir.Root, "credentials");
                var configFile = Path.Combine(testDir.Root, "config");
                File.WriteAllText(credentialsFile, @"
[test-profile]
aws_access_key_id = profile-access-key
aws_secret_access_key = profile-secret-key
");
                File.WriteAllText(configFile, string.Empty);

                using (new EnvironmentVariableScope(
                    ("AWS_SHARED_CREDENTIALS_FILE", credentialsFile),
                    ("AWS_CONFIG_FILE", configFile),
                    ("AWS_ACCESS_KEY_ID", null),
                    ("AWS_SECRET_ACCESS_KEY", null),
                    ("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", null)))
                {
                    var originalProfileLocation = AWSConfigs.AWSProfilesLocation;
                    try
                    {
                        AWSConfigs.AWSProfilesLocation = credentialsFile;
                        var settings = CreateSettings("s3", new JObject
                        {
                            ["type"] = "s3",
                            ["bucketName"] = "profile-bucket",
                            ["region"] = "us-east-1",
                            ["profileName"] = "test-profile"
                        });
                        var cache = new LocalCache();

                        var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

                        var fileSystem = Assert.IsType<AmazonS3FileSystem>(result);
                        fileSystem.Root.AbsoluteUri.Should().Be("https://s3.amazonaws.com/profile-bucket/");
                    }
                    finally
                    {
                        AWSConfigs.AWSProfilesLocation = originalProfileLocation;
                    }
                }
            }
        }

        private static LocalSettings CreateSettings(string name, JObject source)
        {
            if (!source.Properties().Any(e => StringComparer.OrdinalIgnoreCase.Equals(e.Name, "name")))
            {
                source.AddFirst(new JProperty("name", name));
            }

            return new LocalSettings
            {
                Json = new JObject
                {
                    ["sources"] = new JArray(source)
                }
            };
        }

        private static string GetAzureConnectionString()
        {
            return "DefaultEndpointsProtocol=https;AccountName=account;AccountKey=YWJjZA==;EndpointSuffix=core.windows.net";
        }
    }
}