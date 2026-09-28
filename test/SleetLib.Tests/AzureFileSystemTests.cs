using System;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AwesomeAssertions;
using Moq;
using NuGet.Common;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class AzureFileSystemTests
    {
        [Fact]
        public void AzureFileSystem_Constructor_RejectsRootOutsideContainer()
        {
            using (var cache = new LocalCache())
            {
                var service = new Mock<BlobServiceClient>(MockBehavior.Loose);
                AzureUnitTestUtility.CreateContainer(service);

                var ex = Assert.Throws<ArgumentException>(() => new AzureFileSystem(
                    cache,
                    new Uri("https://account.blob.core.windows.net/other/"),
                    new Uri("https://account.blob.core.windows.net/other/"),
                    service.Object,
                    AzureUnitTestUtility.ContainerName));

                ex.Message.Should().Contain("Invalid feed path.");
            }
        }

        [Theory]
        [InlineData("https://account.blob.core.windows.net/feed/sub", null, "sub/")]
        [InlineData("https://account.blob.core.windows.net/feed/", "configured", "configured/")]
        [InlineData("https://account.blob.core.windows.net/feed/sub/", "ignored", "sub/")]
        public void AzureFileSystem_Constructor_NormalizesFeedSubPath(string root, string feedSubPath, string expected)
        {
            using (var cache = new LocalCache())
            {
                var fileSystem = AzureUnitTestUtility.CreateFileSystem(
                    cache,
                    root: root,
                    baseUri: root,
                    feedSubPath: feedSubPath);

                fileSystem.FeedSubPath.Should().Be(expected);
            }
        }

        [Fact]
        public void AzureFileSystem_Get_MapsBlobNamesWithFeedSubPath()
        {
            using (var cache = new LocalCache())
            {
                var fileSystem = AzureUnitTestUtility.CreateFileSystem(
                    cache,
                    root: "https://account.blob.core.windows.net/feed/sub/",
                    baseUri: "https://account.blob.core.windows.net/feed/sub/");
                var blob = AzureUnitTestUtility.CreateBlob(fileSystem.Container, "sub/index.json");

                var file = fileSystem.Get("index.json");

                fileSystem.GetRelativePath(file.EntityUri).Should().Be("sub/index.json");
                file.EntityUri.AbsoluteUri.Should().Be("https://account.blob.core.windows.net/feed/sub/index.json");
                fileSystem.Container.Verify(c => c.GetBlobClient("sub/index.json"), Times.Once());
                blob.Object.Uri.AbsoluteUri.Should().EndWith("sub/index.json");
            }
        }

        [Fact]
        public void AzureFileSystem_Get_MapsBlobNamesWithoutFeedSubPath()
        {
            using (var cache = new LocalCache())
            {
                var fileSystem = AzureUnitTestUtility.CreateFileSystem(cache);
                AzureUnitTestUtility.CreateBlob(fileSystem.Container, "index.json");

                var file = fileSystem.Get("index.json");

                fileSystem.GetRelativePath(file.EntityUri).Should().Be("index.json");
                file.EntityUri.AbsoluteUri.Should().Be("https://account.blob.core.windows.net/feed/index.json");
                fileSystem.Container.Verify(c => c.GetBlobClient("index.json"), Times.Once());
            }
        }

        [Fact]
        public async Task AzureFileSystem_GetFiles_FiltersSubPathSkipsLocksAndReadsMultiplePages()
        {
            using (var cache = new LocalCache())
            {
                var fileSystem = AzureUnitTestUtility.CreateFileSystem(
                    cache,
                    root: "https://account.blob.core.windows.net/feed/sub/",
                    baseUri: "https://account.blob.core.windows.net/feed/sub/");
                AzureUnitTestUtility.CreateBlob(fileSystem.Container, "sub/index.json");
                AzureUnitTestUtility.CreateBlob(fileSystem.Container, "sub/package/index.json");
                fileSystem.Container
                    .Setup(c => c.GetBlobsAsync(It.IsAny<GetBlobsOptions>(), It.IsAny<System.Threading.CancellationToken>()))
                    .Returns(AzureUnitTestUtility.Pages(
                        new[]
                        {
                            BlobsModelFactory.BlobItem(name: "sub/index.json"),
                            BlobsModelFactory.BlobItem(name: "other/index.json"),
                            BlobsModelFactory.BlobItem(name: "sub/" + AzureFileSystemLock.LockFile)
                        },
                        new[] { BlobsModelFactory.BlobItem(name: "sub/package/index.json") }));

                var files = await fileSystem.GetFiles(NullLogger.Instance, TestContext.Current.CancellationToken);

                files.Select(e => e.EntityUri.AbsoluteUri).Should().BeEquivalentTo(new[]
                {
                    "https://account.blob.core.windows.net/feed/sub/index.json",
                    "https://account.blob.core.windows.net/feed/sub/package/index.json"
                });
            }
        }

        [Fact]
        public async Task AzureFileSystem_Validate_ReturnsTrueWhenContainerExists()
        {
            using (var cache = new LocalCache())
            {
                var fileSystem = AzureUnitTestUtility.CreateFileSystem(cache);
                var log = new TestLogger();
                fileSystem.Container.Setup(c => c.ExistsAsync(It.IsAny<System.Threading.CancellationToken>()))
                    .ReturnsAsync(Response.FromValue(true, AzureUnitTestUtility.Response));

                (await fileSystem.Validate(log, TestContext.Current.CancellationToken)).Should().BeTrue();
                log.GetMessages().Should().Contain("Found https://account.blob.core.windows.net/feed/");
            }
        }

        [Fact]
        public async Task AzureFileSystem_Validate_ReturnsFalseWhenContainerIsMissing()
        {
            using (var cache = new LocalCache())
            {
                var fileSystem = AzureUnitTestUtility.CreateFileSystem(cache);
                var log = new TestLogger();
                fileSystem.Container.Setup(c => c.ExistsAsync(It.IsAny<System.Threading.CancellationToken>()))
                    .ReturnsAsync(Response.FromValue(false, AzureUnitTestUtility.Response));

                (await fileSystem.Validate(log, TestContext.Current.CancellationToken)).Should().BeFalse();
                log.GetMessages().Should().Contain("Unable to find https://account.blob.core.windows.net/feed/.");
            }
        }

        [Fact]
        public async Task AzureFileSystem_BucketOperations_UseContainerClient()
        {
            using (var cache = new LocalCache())
            {
                var fileSystem = AzureUnitTestUtility.CreateFileSystem(cache);
                fileSystem.Container.Setup(c => c.ExistsAsync(It.IsAny<System.Threading.CancellationToken>()))
                    .ReturnsAsync(Response.FromValue(true, AzureUnitTestUtility.Response));

                (await fileSystem.HasBucket(NullLogger.Instance, TestContext.Current.CancellationToken)).Should().BeTrue();
                await fileSystem.CreateBucket(NullLogger.Instance, TestContext.Current.CancellationToken);
                await fileSystem.DeleteBucket(NullLogger.Instance, TestContext.Current.CancellationToken);

                fileSystem.Container.Verify(
                    c => c.CreateIfNotExistsAsync(
                        PublicAccessType.BlobContainer,
                        null,
                        null,
                        TestContext.Current.CancellationToken),
                    Times.Once());
                fileSystem.Container.Verify(
                    c => c.DeleteIfExistsAsync(null, TestContext.Current.CancellationToken),
                    Times.Once());
            }
        }
    }
}
