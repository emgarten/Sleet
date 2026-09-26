using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
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

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithoutProvider_UsesAmazonS3Defaults()
        {
            var fileSystem = await CreateS3FileSystemAsync(source =>
            {
                source["region"] = "us-west-2";
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

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithMinioProvider_UsesPathStyle()
        {
            var fileSystem = await CreateS3FileSystemAsync(source =>
            {
                source["provider"] = "minio";
                source["serviceURL"] = "http://localhost:9000";
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
        public async Task CreateFileSystemAsync_WithS3Type_WithMinioProviderAndForcePathStyleFalse_UsesVirtualHostStyle()
        {
            var fileSystem = await CreateS3FileSystemAsync(source =>
            {
                source["provider"] = "minio";
                source["serviceURL"] = "https://minio.example.com";
                source["forcePathStyle"] = false;
                source["checksumMode"] = "WhenRequired";
            });

            var config = GetS3Config(fileSystem);
            config.ForcePathStyle.Should().BeFalse();
            config.RequestChecksumCalculation.Should().Be(RequestChecksumCalculation.WHEN_REQUIRED);
        }

        [Theory]
        [InlineData("r2", "Missing serviceURL for Cloudflare R2 account.")]
        [InlineData("minio", "Missing serviceURL for MinIO account.")]
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

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithUnknownProvider_ThrowsArgumentException()
        {
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "gcs";
                source["serviceURL"] = "https://storage.googleapis.com";
            });

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, new LocalCache(), "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Unknown provider 'gcs' for s3 source. Valid values are: aws, r2, minio", ex.Message);
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

        internal static LocalSettings GetS3Settings(Action<JObject> configure)
        {
            var source = new JObject
            {
                ["name"] = "s3",
                ["type"] = "s3",
                ["bucketName"] = "test-bucket",
                ["accessKeyId"] = "key",
                ["secretAccessKey"] = "secret"
            };

            configure(source);

            return new LocalSettings
            {
                Json = new JObject
                {
                    ["sources"] = new JArray { source }
                }
            };
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