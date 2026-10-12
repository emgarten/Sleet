using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AwesomeAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class AzureFileTests
    {
        public static IEnumerable<object[]> UploadCases
        {
            get
            {
                yield return new object[] { "flatcontainer/a/1.0.0/a.1.0.0.nupkg", "package bytes", "application/zip", "immutable-cache", null, false };
                yield return new object[] { "flatcontainer/a/1.0.0/a.nuspec", "<package />", "application/xml", "immutable-cache", null, false };
                yield return new object[] { "registration/a/index.xml", "<feed />", "application/xml", "immutable-cache", null, false };
                yield return new object[] { "assets/icon.svg", "<svg />", "image/svg+xml", "mutable-cache", null, false };
                yield return new object[] { "flatcontainer/a/1.0.0/icon", "image bytes", "image/png", "immutable-cache", null, false };
                yield return new object[] { "flatcontainer/a/1.0.0/readme", "# readme", "text/markdown", "immutable-cache", null, false };
                yield return new object[] { "index.json", "{ \"b\" : 2 }", "application/json", "mutable-cache", "gzip", true };
                yield return new object[] { "extensionless", "{ \"a\" : 1 }", "application/json", "mutable-cache", "gzip", true };
                yield return new object[] { "symbols/a.dll", "dll bytes", "application/octet-stream", "immutable-cache", null, false };
                yield return new object[] { "symbols/a.pdb", "pdb bytes", "application/octet-stream", "immutable-cache", null, false };
                yield return new object[] { "unknown.bin", "not json", null, "no-store", null, false };
            }
        }

        [Theory]
        [MemberData(nameof(UploadCases))]
        public async Task AzureFile_CopyToSource_SetsHeadersAndBody(
            string blobName,
            string content,
            string contentType,
            string cacheControl,
            string contentEncoding,
            bool gzipped)
        {
            using (var cache = new LocalCache())
            {
                var fileSystem = AzureUnitTestUtility.CreateFileSystem(cache);
                var blob = AzureUnitTestUtility.CreateBlob(fileSystem.Container, blobName);
                var capture = new AzureUploadCapture();
                AzureUnitTestUtility.CaptureUpload(blob, capture);

                await fileSystem.Get(blobName).Write(
                    new MemoryStream(Encoding.UTF8.GetBytes(content)),
                    NullLogger.Instance,
                    TestContext.Current.CancellationToken);
                await fileSystem.Commit(NullLogger.Instance, TestContext.Current.CancellationToken);

                capture.Headers.ContentType.Should().Be(contentType);
                capture.Headers.CacheControl.Should().Be(cacheControl);
                capture.Headers.ContentEncoding.Should().Be(contentEncoding);

                if (gzipped)
                {
                    JObject.Parse(AzureUnitTestUtility.Gunzip(capture.Body)).Should().BeEquivalentTo(JObject.Parse(content));
                }
                else
                {
                    Encoding.UTF8.GetString(capture.Body).Should().Be(content);
                }
            }
        }

        [Fact]
        public async Task AzureFile_CopyToSource_UsesDefaultNoStoreCacheControl()
        {
            using (var cache = new LocalCache())
            {
                var service = new Mock<BlobServiceClient>(MockBehavior.Loose);
                var container = AzureUnitTestUtility.CreateContainer(service);
                var fileSystem = new AzureFileSystem(
                    cache,
                    new System.Uri(AzureUnitTestUtility.ContainerUri),
                    new System.Uri(AzureUnitTestUtility.ContainerUri),
                    service.Object,
                    AzureUnitTestUtility.ContainerName);
                var blob = AzureUnitTestUtility.CreateBlob(container, "index.json");
                var capture = new AzureUploadCapture();
                AzureUnitTestUtility.CaptureUpload(blob, capture);

                await fileSystem.Get("index.json").Write(
                    new JObject(new JProperty("a", 1)),
                    NullLogger.Instance,
                    TestContext.Current.CancellationToken);
                await fileSystem.Commit(NullLogger.Instance, TestContext.Current.CancellationToken);

                capture.Headers.CacheControl.Should().Be("no-store");
            }
        }

        [Fact]
        public async Task AzureFile_CopyFromSource_DecodesGzipContentIntoCache()
        {
            using (var cache = new LocalCache())
            {
                var fileSystem = AzureUnitTestUtility.CreateFileSystem(cache);
                var blob = AzureUnitTestUtility.CreateBlob(fileSystem.Container, "index.json");
                AzureUnitTestUtility.DownloadTo(blob, AzureUnitTestUtility.GZip("{\"a\":1}"), "gzip");

                var file = fileSystem.Get("index.json");
                using (var stream = await file.GetStream(NullLogger.Instance, TestContext.Current.CancellationToken))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).Should().Be("{\"a\":1}");
                }
            }
        }

        [Fact]
        public async Task AzureFile_CopyFromSource_CopiesPlainContentIntoCache()
        {
            using (var cache = new LocalCache())
            {
                var fileSystem = AzureUnitTestUtility.CreateFileSystem(cache);
                var blob = AzureUnitTestUtility.CreateBlob(fileSystem.Container, "index.json");
                AzureUnitTestUtility.DownloadTo(blob, Encoding.UTF8.GetBytes("{\"plain\":true}"));

                var file = fileSystem.Get("index.json");
                using (var stream = await file.GetStream(NullLogger.Instance, TestContext.Current.CancellationToken))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).Should().Be("{\"plain\":true}");
                }
            }
        }

        [Fact]
        public async Task AzureFile_RemoteExists_UsesBlobExists()
        {
            using (var cache = new LocalCache())
            {
                var fileSystem = AzureUnitTestUtility.CreateFileSystem(cache);
                var blob = AzureUnitTestUtility.CreateBlob(fileSystem.Container, "missing.json");
                blob.Setup(b => b.ExistsAsync(It.IsAny<System.Threading.CancellationToken>()))
                    .ReturnsAsync(Response.FromValue(false, AzureUnitTestUtility.Response));

                (await fileSystem.Get("missing.json").Exists(NullLogger.Instance, TestContext.Current.CancellationToken)).Should().BeFalse();
            }
        }
    }
}
