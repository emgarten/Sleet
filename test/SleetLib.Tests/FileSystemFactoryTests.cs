using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
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
        public async Task CreateFileSystemAsync_WithS3Type_WithRegionAndServiceURL_UsesRegionToSignRequests()
        {
            var fileSystem = await CreateS3FileSystemAsync(source =>
            {
                source["region"] = "fr-par";
                source["serviceURL"] = "https://s3.fr-par.scw.cloud";
            });

            var config = GetS3Config(fileSystem);
            config.AuthenticationRegion.Should().Be("fr-par");
            config.RegionEndpoint.Should().BeNull();
            fileSystem.Root.AbsoluteUri.Should().Be("https://s3.fr-par.scw.cloud/test-bucket/");
            fileSystem.BaseURI.AbsoluteUri.Should().Be("https://s3.fr-par.scw.cloud/test-bucket/");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("aws")]
        [InlineData(" ")]
        public async Task CreateFileSystemAsync_WithS3Type_WithDefaultProvider_UsesAmazonS3Defaults(string provider)
        {
            var fileSystem = await CreateS3FileSystemAsync(source =>
            {
                source["region"] = "us-west-2";

                if (provider != null)
                {
                    source["provider"] = provider;
                }
            });

            var config = GetS3Config(fileSystem);
            config.RegionEndpoint.Should().Be(Amazon.RegionEndpoint.USWest2);
            config.ForcePathStyle.Should().BeFalse();
            config.RequestChecksumCalculation.Should().Be(RequestChecksumCalculation.WHEN_SUPPORTED);
            GetDisablePayloadSigning(fileSystem).Should().BeFalse();
        }

        [Theory]
        [InlineData("r2")]
        [InlineData("R2")]
        [InlineData(" r2 ")]
        public async Task CreateFileSystemAsync_WithS3Type_WithCloudflareR2Provider_UsesR2Defaults(string provider)
        {
            var fileSystem = await CreateS3FileSystemAsync(source =>
            {
                source["provider"] = provider;
                source["serviceURL"] = "https://account.r2.cloudflarestorage.com";
                source["baseURI"] = "https://nuget.example.com/";
            });

            var config = GetS3Config(fileSystem);
            config.ForcePathStyle.Should().BeFalse();
            config.RequestChecksumCalculation.Should().Be(RequestChecksumCalculation.WHEN_REQUIRED);
            config.ResponseChecksumValidation.Should().Be(ResponseChecksumValidation.WHEN_REQUIRED);
            GetDisablePayloadSigning(fileSystem).Should().BeTrue();
            fileSystem.Root.AbsoluteUri.Should().Be("https://account.r2.cloudflarestorage.com/test-bucket/");
            fileSystem.BaseURI.AbsoluteUri.Should().Be("https://nuget.example.com/");
        }

        [Theory]
        [InlineData("http://localhost:9000")]
        [InlineData("http://localhost:9000/")]
        public async Task CreateFileSystemAsync_WithS3Type_WithSelfHostedProvider_UsesPathStyle(string serviceURL)
        {
            var fileSystem = await CreateS3FileSystemAsync(source =>
            {
                source["provider"] = "self-hosted";
                source["serviceURL"] = serviceURL;
            });

            var config = GetS3Config(fileSystem);
            config.ForcePathStyle.Should().BeTrue();
            config.RequestChecksumCalculation.Should().Be(RequestChecksumCalculation.WHEN_SUPPORTED);
            GetDisablePayloadSigning(fileSystem).Should().BeFalse();
            fileSystem.Root.AbsoluteUri.Should().Be("http://localhost:9000/test-bucket/");
            fileSystem.BaseURI.AbsoluteUri.Should().Be("http://localhost:9000/test-bucket/");
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithSettings_OverridesProviderDefaults()
        {
            var fileSystem = await CreateS3FileSystemAsync(source =>
            {
                source["provider"] = "r2";
                source["serviceURL"] = "https://account.r2.cloudflarestorage.com";
                source["baseURI"] = "https://nuget.example.com/";
                source["disablePayloadSigning"] = false;
                source["checksumMode"] = "whenSupported";
                source["forcePathStyle"] = true;
            });

            var config = GetS3Config(fileSystem);
            config.ForcePathStyle.Should().BeTrue();
            config.RequestChecksumCalculation.Should().Be(RequestChecksumCalculation.WHEN_SUPPORTED);
            config.ResponseChecksumValidation.Should().Be(ResponseChecksumValidation.WHEN_SUPPORTED);
            GetDisablePayloadSigning(fileSystem).Should().BeFalse();
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithSelfHostedProviderAndForcePathStyleFalse_UsesVirtualHostStyle()
        {
            var fileSystem = await CreateS3FileSystemAsync(source =>
            {
                source["provider"] = "self-hosted";
                source["serviceURL"] = "https://s3.example.com";
                source["forcePathStyle"] = false;
                source["checksumMode"] = "WhenRequired";
            });

            var config = GetS3Config(fileSystem);
            config.ForcePathStyle.Should().BeFalse();
            config.RequestChecksumCalculation.Should().Be(RequestChecksumCalculation.WHEN_REQUIRED);
            config.ResponseChecksumValidation.Should().Be(ResponseChecksumValidation.WHEN_REQUIRED);
        }

        [Theory]
        [InlineData("r2", "Missing serviceURL for Cloudflare R2 account.")]
        [InlineData("self-hosted", "Missing serviceURL for self-hosted S3 account.")]
        public async Task CreateFileSystemAsync_WithS3Type_WithProviderAndWithoutServiceURL_ThrowsArgumentException(string provider, string message)
        {
            var settings = GetS3Settings(source =>
            {
                source["provider"] = provider;
                source["region"] = "us-east-1";
                source["baseURI"] = "https://nuget.example.com/";
            });

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, new LocalCache(), "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains(message, ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("https://account.r2.cloudflarestorage.com/test-bucket/")]
        public async Task CreateFileSystemAsync_WithS3Type_WithCloudflareR2ProviderAndWithoutBaseURI_ThrowsArgumentException(string path)
        {
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "r2";
                source["serviceURL"] = "https://account.r2.cloudflarestorage.com";

                if (path != null)
                {
                    source["path"] = path;
                }
            });

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, new LocalCache(), "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Missing baseURI for Cloudflare R2 account.", ex.Message);
        }

        [Theory]
        [InlineData("2")]
        [InlineData("default")]
        [InlineData("when_required")]
        public async Task CreateFileSystemAsync_WithS3Type_WithInvalidChecksumMode_ThrowsArgumentException(string checksumMode)
        {
            var settings = GetS3Settings(source =>
            {
                source["region"] = "us-east-1";
                source["checksumMode"] = checksumMode;
            });

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, new LocalCache(), "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains($"Invalid checksumMode '{checksumMode}'. Valid values are: whenSupported, whenRequired", ex.Message);
        }

        [Theory]
        [InlineData("gcs")]
        [InlineData(" gcs ")]
        public async Task CreateFileSystemAsync_WithS3Type_WithUnknownProvider_ThrowsArgumentException(string provider)
        {
            var settings = GetS3Settings(source =>
            {
                source["provider"] = provider;
                source["serviceURL"] = "https://storage.googleapis.com";
            });

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, new LocalCache(), "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Unknown provider 'gcs' for s3 source. Valid values are: aws, r2, self-hosted", ex.Message);
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
        public async Task CreateFileSystemAsync_WithS3Type_WithServiceURLAndDefaultCredentials_CreatesAmazonS3FileSystem()
        {
            using (var testDir = new TestFolder())
            {
                var credentialsFile = Path.Combine(testDir.Root, "credentials");
                var configFile = Path.Combine(testDir.Root, "config");
                File.WriteAllText(credentialsFile, @"
[default]
aws_access_key_id = default-access-key
aws_secret_access_key = default-secret-key
");
                File.WriteAllText(configFile, string.Empty);

                using (new EnvironmentVariableScope(
                    ("AWS_SHARED_CREDENTIALS_FILE", credentialsFile),
                    ("AWS_CONFIG_FILE", configFile),
                    ("AWS_EC2_METADATA_DISABLED", "true")))
                {
                    var settings = CreateSettings("s3", new JObject
                    {
                        ["type"] = "s3",
                        ["provider"] = "self-hosted",
                        ["bucketName"] = "test-bucket",
                        ["serviceURL"] = "http://localhost:9000"
                    });

                    // Fake credentials would fail an STS check, it must be skipped without a region endpoint
                    var result = await FileSystemFactory.CreateFileSystemAsync(settings, new LocalCache(), "s3", NullLogger.Instance);

                    var fileSystem = Assert.IsType<AmazonS3FileSystem>(result);
                    GetS3Config(fileSystem).RegionEndpoint.Should().BeNull();
                    fileSystem.Root.AbsoluteUri.Should().Be("http://localhost:9000/test-bucket/");
                }
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

        internal static LocalSettings GetS3Settings(Action<JObject> configure)
        {
            var source = new JObject
            {
                ["type"] = "s3",
                ["bucketName"] = "test-bucket",
                ["accessKeyId"] = "key",
                ["secretAccessKey"] = "secret"
            };

            configure(source);

            return CreateSettings("s3", source);
        }

        internal static async Task<AmazonS3FileSystem> CreateS3FileSystemAsync(Action<JObject> configure)
        {
            var result = await FileSystemFactory.CreateFileSystemAsync(GetS3Settings(configure), new LocalCache(), "s3", NullLogger.Instance);

            return result.Should().BeOfType<AmazonS3FileSystem>().Subject;
        }

        private static AmazonS3Config GetS3Config(AmazonS3FileSystem fileSystem)
        {
            var client = (IAmazonS3)GetPrivateField(fileSystem, "_client");

            return (AmazonS3Config)client.Config;
        }

        private static bool GetDisablePayloadSigning(AmazonS3FileSystem fileSystem)
        {
            return (bool)GetPrivateField(fileSystem, "_disablePayloadSigning");
        }

        private static object GetPrivateField(AmazonS3FileSystem fileSystem, string name)
        {
            return typeof(AmazonS3FileSystem)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(fileSystem);
        }
    }
}