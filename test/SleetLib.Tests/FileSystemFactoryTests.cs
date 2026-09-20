using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
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
            Assert.Contains("Either 'region' or 'serviceURL' must be specified for Amazon S3", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithBothRegionAndServiceURL_UsesRegionForSigning()
        {
            // Services such as MinIO require both, the region is used as the SigV4 signing region.
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
                        ["serviceURL"] = "https://s3.example.com",
                        ["accessKeyId"] = "key",
                        ["secretAccessKey"] = "secret"
                    }
                }
            };
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            result.Should().BeOfType<AmazonS3FileSystem>();
            ((FileSystemBase)result).Root.AbsoluteUri.Should().Be("https://s3.example.com/test-bucket/");
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
        public async Task CreateFileSystemAsync_WithS3Type_WithUnknownProvider_ThrowsArgumentException()
        {
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "notarealservice";
            });
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Unknown provider 'notarealservice'", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithInvalidChecksumMode_ThrowsArgumentException()
        {
            var settings = GetS3Settings(source =>
            {
                source["checksumMode"] = "sometimes";
            });
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Invalid checksumMode 'sometimes'", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithS3Type_WithInvalidPublicAccess_ThrowsArgumentException()
        {
            var settings = GetS3Settings(source =>
            {
                source["publicAccess"] = "everyone";
            });
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Invalid publicAccess 'everyone'", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithMinioProvider_CreatesFileSystem()
        {
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "minio";
                source["region"] = "us-east-1";
                source["serviceURL"] = "http://localhost:9000";
            });
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            result.Should().BeOfType<AmazonS3FileSystem>();
            ((FileSystemBase)result).Root.AbsoluteUri.Should().Be("http://localhost:9000/test-bucket/");
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithYandexProvider_WithAes256_ThrowsArgumentException()
        {
            // Yandex supports aws:kms only, AES256 would fail at push time.
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "yandex";
                source["serviceURL"] = "https://s3.yandexcloud.net";
                source["serverSideEncryptionMethod"] = "AES256";
            });
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Option 'serverSideEncryptionMethod' is not supported by Yandex Object Storage", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithR2Provider_CreatesFileSystem()
        {
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "r2";
                source["accountId"] = "abc123";
                source["baseURI"] = "https://nuget.example.com/";
            });
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            result.Should().BeOfType<AmazonS3FileSystem>();
            ((FileSystemBase)result).Root.AbsoluteUri.Should().Be("https://abc123.r2.cloudflarestorage.com/test-bucket/");
            result.BaseURI.AbsoluteUri.Should().Be("https://nuget.example.com/");
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithR2Provider_WithJurisdiction_UsesJurisdictionEndpoint()
        {
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "cloudflare";
                source["accountId"] = "abc123";
                source["jurisdiction"] = "eu";
                source["baseURI"] = "https://nuget.example.com/";
            });
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            ((FileSystemBase)result).Root.AbsoluteUri.Should().Be("https://abc123.eu.r2.cloudflarestorage.com/test-bucket/");
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithR2Provider_WithoutBaseURI_ThrowsArgumentException()
        {
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "r2";
                source["accountId"] = "abc123";
            });
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Missing baseURI for Cloudflare R2 account", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithR2Provider_WithoutAccountId_ThrowsArgumentException()
        {
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "r2";
                source["baseURI"] = "https://nuget.example.com/";
            });
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Missing accountId for Cloudflare R2 account", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithR2Provider_WithAcl_ThrowsArgumentException()
        {
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "r2";
                source["accountId"] = "abc123";
                source["baseURI"] = "https://nuget.example.com/";
                source["acl"] = "PublicRead";
            });
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Option 'acl' is not supported by Cloudflare R2", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithR2Provider_WithExplicitServiceURL_UsesServiceURL()
        {
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "r2";
                source["serviceURL"] = "https://custom.example.com";
                source["baseURI"] = "https://nuget.example.com/";
            });
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            ((FileSystemBase)result).Root.AbsoluteUri.Should().Be("https://custom.example.com/test-bucket/");
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithNonAwsProvider_WithoutServiceURL_ThrowsArgumentException()
        {
            // 'region' only identifies an endpoint for Amazon S3. Without this a missing or
            // misspelled serviceURL would silently send requests to Amazon.
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "minio";
                source["region"] = "us-east-1";
            });
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Missing serviceURL for MinIO", ex.Message);
            Assert.Contains("http://localhost:9000", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithR2Provider_WithRegion_ThrowsArgumentException()
        {
            // R2 always signs with the auto region, a region copied from an Amazon S3 config
            // would break signing.
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "r2";
                source["accountId"] = "abc123";
                source["baseURI"] = "https://nuget.example.com/";
                source["region"] = "us-east-1";
            });
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Option 'region' is not supported by Cloudflare R2", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithR2Provider_WithPathButNoBaseURI_ThrowsArgumentException()
        {
            // 'path' falls back to the bucket url, which is the private API endpoint for R2.
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "r2";
                source["accountId"] = "abc123";
                source["path"] = "https://abc123.r2.cloudflarestorage.com/test-bucket/";
            });
            var cache = new LocalCache();

            Func<Task> act = async () => await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var ex = await Assert.ThrowsAsync<ArgumentException>(act);
            Assert.Contains("Missing baseURI for Cloudflare R2 account", ex.Message);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithCannedAclProvider_DefaultsObjectAclToPublicRead()
        {
            // Access is granted by acl rather than a bucket policy, so uploaded objects need the
            // same acl as the bucket to be readable.
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "b2";
                source["serviceURL"] = "https://s3.us-west-004.backblazeb2.com";
            });
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var acl = (S3CannedACL)typeof(AmazonS3FileSystem)
                .GetField("_acl", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(result);

            acl.Should().Be(S3CannedACL.PublicRead);
        }

        [Fact]
        public async Task CreateFileSystemAsync_WithBucketPolicyProvider_LeavesObjectAclUnset()
        {
            var settings = GetS3Settings(source =>
            {
                source["provider"] = "minio";
                source["serviceURL"] = "http://localhost:9000";
            });
            var cache = new LocalCache();

            var result = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "s3", NullLogger.Instance);

            var acl = typeof(AmazonS3FileSystem)
                .GetField("_acl", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(result);

            acl.Should().BeNull();
        }

        /// <summary>
        /// Minimal valid s3 source with explicit credentials, customized by the caller.
        /// Tests add 'region' or 'serviceURL' as needed.
        /// </summary>
        private static LocalSettings GetS3Settings(Action<JObject> configure)
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

            return new LocalSettings()
            {
                Json = new JObject
                {
                    ["sources"] = new JArray { source }
                }
            };
        }
    }
}