using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Azure;
using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Moq;
using Moq.Protected;
using Sleet;

namespace SleetLib.Tests
{
    internal static class AzureUnitTestUtility
    {
        public const string ContainerName = "feed";
        public const string ContainerUri = "https://account.blob.core.windows.net/feed/";
        public static readonly Response Response = new AzureTestResponse();

        public static AzureTestFileSystem CreateFileSystem(
            LocalCache cache,
            string root = ContainerUri,
            string baseUri = ContainerUri,
            string feedSubPath = null,
            string immutableCacheControl = "immutable-cache",
            string mutableCacheControl = "mutable-cache")
        {
            var service = new Mock<BlobServiceClient>(MockBehavior.Loose);
            var container = CreateContainer(service);

            return new AzureTestFileSystem(
                cache,
                new Uri(root),
                new Uri(baseUri),
                service.Object,
                ContainerName,
                container,
                feedSubPath,
                immutableCacheControl,
                mutableCacheControl);
        }

        public static Mock<BlobContainerClient> CreateContainer(Mock<BlobServiceClient> service)
        {
            var container = new Mock<BlobContainerClient>(MockBehavior.Loose);
            container.SetupGet(c => c.Uri).Returns(new Uri(ContainerUri));
            service.Setup(s => s.GetBlobContainerClient(ContainerName)).Returns(container.Object);
            return container;
        }

        public static Mock<BlobClient> CreateBlob(Mock<BlobContainerClient> container, string blobName)
        {
            var blob = new Mock<BlobClient>(MockBehavior.Loose);
            blob.SetupGet(b => b.Uri).Returns(new Uri(ContainerUri + blobName));
            container.Setup(c => c.GetBlobClient(blobName)).Returns(blob.Object);
            return blob;
        }

        public static void CaptureUpload(Mock<BlobClient> blob, AzureUploadCapture capture)
        {
            // Expression trees cannot omit optional arguments, so every parameter of the overload AzureFile calls is matched.
            blob.Setup(b => b.UploadAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<BlobHttpHeaders>(),
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<IProgress<long>>(),
                    It.IsAny<AccessTier?>(),
                    It.IsAny<StorageTransferOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback(new InvocationAction(invocation =>
                {
                    capture.Body = ReadAllBytes((Stream)invocation.Arguments[0]);
                    capture.Headers = (BlobHttpHeaders)invocation.Arguments[1];
                }))
                .ReturnsAsync(Azure.Response.FromValue<BlobContentInfo>(null, Response));
        }

        public static void DownloadTo(Mock<BlobClient> blob, byte[] content, string contentEncoding = null)
        {
            blob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Azure.Response.FromValue(true, Response));
            blob.Setup(b => b.DownloadToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, CancellationToken>((stream, _) => stream.Write(content, 0, content.Length))
                .ReturnsAsync(Response);
            blob.Setup(b => b.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Azure.Response.FromValue(
                    BlobsModelFactory.BlobProperties(contentEncoding: contentEncoding),
                    Response));
        }

        public static byte[] GZip(string text)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
                using (var writer = new StreamWriter(gzip, Encoding.UTF8))
                {
                    writer.Write(text);
                }

                return output.ToArray();
            }
        }

        public static string Gunzip(byte[] bytes)
        {
            using (var input = new MemoryStream(bytes))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        public static byte[] ReadAllBytes(Stream stream)
        {
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        public static void ConfigureLeaseClient(Mock<BlobClient> blob, Mock<BlobLeaseClient> leaseClient, Action<string> onLeaseId = null)
        {
            // GetBlobLeaseClient is an extension method that calls the protected virtual GetBlobLeaseClientCore,
            // so the mock is configured through the core method.
            blob.Protected()
                .Setup<BlobLeaseClient>("GetBlobLeaseClientCore", ItExpr.IsAny<string>())
                .Callback<string>(leaseId => onLeaseId?.Invoke(leaseId))
                .Returns(leaseClient.Object);
        }

        public static void ConfigureSuccessfulLease(Mock<BlobClient> blob, Mock<BlobLeaseClient> leaseClient)
        {
            var leaseId = string.Empty;
            ConfigureLeaseClient(blob, leaseClient, id => leaseId = id);

            leaseClient.Setup(l => l.AcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Azure.Response.FromValue(
                    BlobsModelFactory.BlobLease(new ETag("\"etag\""), DateTimeOffset.UtcNow, leaseId),
                    Response));
            leaseClient.Setup(l => l.RenewAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Azure.Response.FromValue(
                    BlobsModelFactory.BlobLease(new ETag("\"etag\""), DateTimeOffset.UtcNow, leaseId),
                    Response));
            leaseClient.Setup(l => l.ReleaseAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Azure.Response.FromValue<ReleasedObjectInfo>(null, Response));
        }

        public static AsyncPageable<BlobItem> Pages(params IEnumerable<BlobItem>[] pages)
        {
            return AsyncPageable<BlobItem>.FromPages(
                pages.Select((items, index) => Page<BlobItem>.FromValues(
                    items.ToList(),
                    index + 1 == pages.Length ? null : $"page-{index + 1}",
                    Response)));
        }
    }

    internal sealed class AzureTestResponse : Response
    {
        public override int Status => 200;

        public override string ReasonPhrase => "OK";

        public override Stream ContentStream { get; set; }

        public override string ClientRequestId { get; set; }

        public override void Dispose()
        {
        }

        protected override bool ContainsHeader(string name)
        {
            return false;
        }

        protected override IEnumerable<HttpHeader> EnumerateHeaders()
        {
            return Enumerable.Empty<HttpHeader>();
        }

        protected override bool TryGetHeader(string name, out string value)
        {
            value = null;
            return false;
        }

        protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
        {
            values = null;
            return false;
        }
    }

    internal sealed class AzureUploadCapture
    {
        public BlobHttpHeaders Headers { get; set; }

        public byte[] Body { get; set; }
    }

    internal sealed class AzureTestFileSystem : AzureFileSystem
    {
        public AzureTestFileSystem(
            LocalCache cache,
            Uri root,
            Uri baseUri,
            BlobServiceClient blobServiceClient,
            string containerName,
            Mock<BlobContainerClient> container,
            string feedSubPath,
            string immutableCacheControl,
            string mutableCacheControl)
            : base(cache, root, baseUri, blobServiceClient, containerName, feedSubPath, immutableCacheControl, mutableCacheControl)
        {
            Container = container;
        }

        public Mock<BlobContainerClient> Container { get; }
    }
}
