using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class AmazonS3FileTests
    {
        public static IEnumerable<object[]> UploadCases
        {
            get
            {
                yield return new object[] { "packages/a.1.0.0.nupkg", "nupkg", "application/zip", null, "immutable", false };
                yield return new object[] { "a.nuspec", "nuspec", "application/xml", null, "immutable", false };
                yield return new object[] { "a.xml", "xml", "application/xml", null, "immutable", false };
                yield return new object[] { "a.svg", "svg", "image/svg+xml", null, "mutable", false };
                yield return new object[] { "package/icon", "icon", "image/png", null, "immutable", false };
                yield return new object[] { "package/readme", "readme", "text/markdown", null, "immutable", false };
                yield return new object[] { "index.json", "{ \"hello\": \"world\" }", "application/json", "gzip", "mutable", true };
                yield return new object[] { "json-content", "{ \"hello\": \"world\" }", "application/json", "gzip", "mutable", true };
                yield return new object[] { "lib/a.dll", "dll", "application/octet-stream", null, "immutable", false };
                yield return new object[] { "lib/a.pdb", "pdb", "application/octet-stream", null, "immutable", false };
                yield return new object[] { "a.unknown", "unknown", null, null, "no-store", false };
            }
        }

        [Theory]
        [MemberData(nameof(UploadCases))]
        public async Task AmazonS3File_CopyToSource_UploadsExpectedHeadersAndBody(string path, string content, string contentType, string contentEncoding, string cacheControl, bool isCompressed)
        {
            using (var cache = new LocalCache())
            {
                var client = AmazonS3TestUtility.CreateClient();
                var captured = (PutObjectRequest)null;
                var body = (byte[])null;
                client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                    .Callback<PutObjectRequest, CancellationToken>((r, _) =>
                    {
                        captured = r;
                        body = AmazonS3TestUtility.ReadAll(r.InputStream);
                    })
                    .ReturnsAsync(new PutObjectResponse());
                var root = AmazonS3TestUtility.RootUri();
                var fileSystem = new AmazonS3FileSystem(cache, root, root, client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None, immutableCacheControl: "immutable", mutableCacheControl: "mutable");
                var log = new TestLogger();

                await fileSystem.Get(path).Write(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), log, TestContext.Current.CancellationToken);
                await fileSystem.Commit(log, TestContext.Current.CancellationToken);

                captured.Should().NotBeNull();
                captured.BucketName.Should().Be(AmazonS3TestUtility.BucketName);
                captured.Key.Should().Be(path);
                captured.Headers.ContentType.Should().Be(contentType);
                captured.Headers.ContentEncoding.Should().Be(contentEncoding);
                captured.Headers.CacheControl.Should().Be(cacheControl);
                if (isCompressed)
                {
                    var actualJson = JObject.Parse(AmazonS3TestUtility.ReadGzipUtf8(body));
                    actualJson["hello"].ToString().Should().Be("world");
                }
                else
                {
                    AmazonS3TestUtility.ReadUtf8(body).Should().Be(content);
                }

                if (contentType == null)
                {
                    log.GetMessages().Should().Contain("Unknown file type");
                }
            }
        }

        [Fact]
        public async Task AmazonS3File_CopyToSource_CompressFalseDoesNotGzipJson()
        {
            using (var cache = new LocalCache())
            {
                var client = AmazonS3TestUtility.CreateClient();
                var captured = (PutObjectRequest)null;
                var body = (byte[])null;
                client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                    .Callback<PutObjectRequest, CancellationToken>((r, _) =>
                    {
                        captured = r;
                        body = AmazonS3TestUtility.ReadAll(r.InputStream);
                    })
                    .ReturnsAsync(new PutObjectResponse());
                var root = AmazonS3TestUtility.RootUri();
                var fileSystem = new AmazonS3FileSystem(cache, root, root, client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None, compress: false, mutableCacheControl: "max-age=60");
                var log = new TestLogger();

                await fileSystem.Get("index.json").Write(new JObject { ["hello"] = "world" }, log, TestContext.Current.CancellationToken);
                await fileSystem.Commit(log, TestContext.Current.CancellationToken);

                captured.Headers.ContentType.Should().Be("application/json");
                captured.Headers.ContentEncoding.Should().BeNull();
                captured.Headers.CacheControl.Should().Be("max-age=60");
                JObject.Parse(AmazonS3TestUtility.ReadUtf8(body))["hello"].ToString().Should().Be("world");
            }
        }

        [Fact]
        public async Task AmazonS3File_CopyToSource_SendsAclEncryptionAndDisablePayloadSigning()
        {
            using (var cache = new LocalCache())
            {
                var client = AmazonS3TestUtility.CreateClient();
                var captured = (PutObjectRequest)null;
                client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                    .Callback<PutObjectRequest, CancellationToken>((r, _) => captured = r)
                    .ReturnsAsync(new PutObjectResponse());
                var root = AmazonS3TestUtility.RootUri();
                var fileSystem = new AmazonS3FileSystem(cache, root, root, client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.AES256, acl: S3CannedACL.PublicRead, disablePayloadSigning: true);
                var log = new TestLogger();

                await fileSystem.Get("package.nupkg").Write(new MemoryStream(new byte[] { 1, 2, 3 }), log, TestContext.Current.CancellationToken);
                await fileSystem.Commit(log, TestContext.Current.CancellationToken);

                captured.CannedACL.Should().Be(S3CannedACL.PublicRead);
                captured.ServerSideEncryptionMethod.Should().Be(ServerSideEncryptionMethod.AES256);
                captured.DisablePayloadSigning.Should().BeTrue();
            }
        }

        [Fact]
        public async Task AmazonS3File_CopyFromSource_DecodesGzipObjectIntoLocalCache()
        {
            using (var cache = new LocalCache())
            {
                var client = AmazonS3TestUtility.CreateClient();
                client.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ListObjectsV2Response { S3Objects = new List<S3Object> { new S3Object { Key = "index.json" } } });
                client.Setup(c => c.GetObjectAsync(AmazonS3TestUtility.BucketName, "index.json", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new GetObjectResponse
                    {
                        ResponseStream = new MemoryStream(AmazonS3TestUtility.GzipUtf8("{\"hello\":\"world\"}")),
                        Headers = { ContentEncoding = "gzip" }
                    });
                var root = AmazonS3TestUtility.RootUri();
                var fileSystem = new AmazonS3FileSystem(cache, root, root, client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None);
                var log = new TestLogger();

                var json = await fileSystem.Get("index.json").GetJson(log, TestContext.Current.CancellationToken);

                json["hello"].ToString().Should().Be("world");
            }
        }

        [Fact]
        public async Task AmazonS3File_CopyFromSource_CopiesPlainObjectAsIs()
        {
            using (var cache = new LocalCache())
            {
                var client = AmazonS3TestUtility.CreateClient();
                client.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ListObjectsV2Response { S3Objects = new List<S3Object> { new S3Object { Key = "plain.txt" } } });
                client.Setup(c => c.GetObjectAsync(AmazonS3TestUtility.BucketName, "plain.txt", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new GetObjectResponse { ResponseStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("plain")) });
                var root = AmazonS3TestUtility.RootUri();
                var fileSystem = new AmazonS3FileSystem(cache, root, root, client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None);
                var log = new TestLogger();

                using (var stream = await fileSystem.Get("plain.txt").GetStream(log, TestContext.Current.CancellationToken))
                using (var reader = new StreamReader(stream))
                {
                    reader.ReadToEnd().Should().Be("plain");
                }
            }
        }

        [Theory]
        [InlineData("a.json", true)]
        [InlineData("b.json", false)]
        [InlineData(null, false)]
        public async Task AmazonS3File_RemoteExists_UsesListObjectsV2ExactKeyMatch(string returnedKey, bool expected)
        {
            using (var cache = new LocalCache())
            {
                var client = AmazonS3TestUtility.CreateClient();
                var response = returnedKey == null ? null : new ListObjectsV2Response { S3Objects = new List<S3Object> { new S3Object { Key = returnedKey } } };
                client.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(response);
                var root = AmazonS3TestUtility.RootUri();
                var fileSystem = new AmazonS3FileSystem(cache, root, root, client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None);

                var exists = await fileSystem.Get("a.json").Exists(new TestLogger(), TestContext.Current.CancellationToken);

                exists.Should().Be(expected);
                client.Verify(
                    c => c.ListObjectsV2Async(
                        It.Is<ListObjectsV2Request>(r => r.BucketName == AmazonS3TestUtility.BucketName && r.Prefix == "a.json"),
                        It.IsAny<CancellationToken>()),
                    Times.Once());
            }
        }
    }
}
