using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using AwesomeAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class AzureFileSystemLockTests
    {
        [Fact]
        public async Task AzureFileSystemLock_TryObtainLockAsync_AcquiresLeaseAndWritesMessage()
        {
            var log = new TestLogger();
            var blob = new Mock<BlobClient>(MockBehavior.Loose);
            var messageBlob = new Mock<BlobClient>(MockBehavior.Loose);
            var leaseClient = new Mock<BlobLeaseClient>(MockBehavior.Loose);
            var uploadText = string.Empty;
            AzureUnitTestUtility.ConfigureSuccessfulLease(blob, leaseClient);
            blob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(false, AzureUnitTestUtility.Response));
            // AzureFileSystemLock creates the lock blob with the overload without a cancellation token.
#pragma warning disable xUnit1051
            blob.Setup(b => b.UploadAsync(It.IsAny<BinaryData>()))
#pragma warning restore xUnit1051
                .ReturnsAsync(Response.FromValue<BlobContentInfo>(null, AzureUnitTestUtility.Response));
            messageBlob.Setup(b => b.UploadAsync(It.IsAny<BinaryData>(), true, It.IsAny<CancellationToken>()))
                .Callback<BinaryData, bool, CancellationToken>((data, _, _) => uploadText = data.ToString())
                .ReturnsAsync(Response.FromValue<BlobContentInfo>(null, AzureUnitTestUtility.Response));
            messageBlob.Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, AzureUnitTestUtility.Response));

            using (var fileLock = new AzureFastFileSystemLock(blob.Object, messageBlob.Object, log))
            {
                (await fileLock.GetLock(TimeSpan.Zero, "holder", TestContext.Current.CancellationToken)).Should().BeTrue();
                fileLock.IsLocked.Should().BeTrue();

                var message = JObject.Parse(uploadText);
                message["message"].ToString().Should().Be("holder");

                fileLock.Release();
            }

#pragma warning disable xUnit1051
            blob.Verify(b => b.UploadAsync(It.IsAny<BinaryData>()), Times.Once());
#pragma warning restore xUnit1051
            leaseClient.Verify(l => l.AcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()));
            messageBlob.Verify(
                b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()),
                Times.Once());
        }

        [Fact]
        public async Task AzureFileSystemLock_TryObtainLockAsync_WhenLeaseIsHeldReturnsFalseWithHolderMessage()
        {
            var log = new TestLogger();
            var blob = new Mock<BlobClient>(MockBehavior.Loose);
            var messageBlob = new Mock<BlobClient>(MockBehavior.Loose);
            var leaseClient = new Mock<BlobLeaseClient>(MockBehavior.Loose);
            AzureUnitTestUtility.ConfigureLeaseClient(blob, leaseClient);
            blob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, AzureUnitTestUtility.Response));
            leaseClient.Setup(l => l.AcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(
                    BlobsModelFactory.BlobLease(new ETag("\"etag\""), DateTimeOffset.UtcNow, "another-lease"),
                    AzureUnitTestUtility.Response));
            // AzureFileSystemLock calls the overload without a cancellation token.
#pragma warning disable xUnit1051
            messageBlob.Setup(b => b.DownloadContentAsync())
#pragma warning restore xUnit1051
                .ReturnsAsync(Response.FromValue(
                    BlobsModelFactory.BlobDownloadResult(BinaryData.FromString(new JObject(
                        new JProperty("date", "2026-01-01T00:00:00.0000000Z"),
                        new JProperty("message", "other client")).ToString())),
                    AzureUnitTestUtility.Response));

            using (var fileLock = new AzureFastFileSystemLock(blob.Object, messageBlob.Object, log))
            {
                (await fileLock.GetLock(TimeSpan.Zero, "waiter", TestContext.Current.CancellationToken)).Should().BeFalse();
            }

            var messages = log.GetMessages();
            messages.Should().Contain("Feed is locked by: other client since:");
            messages.Should().Contain("Unable to obtain a lock on the feed.");
            messageBlob.Verify(
                b => b.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [Fact]
        public async Task AzureFileSystemLock_ReleaseAndDispose_StopRenewAndReleaseLease()
        {
            var log = new TestLogger();
            var blob = new Mock<BlobClient>(MockBehavior.Loose);
            var messageBlob = new Mock<BlobClient>(MockBehavior.Loose);
            var leaseClient = new Mock<BlobLeaseClient>(MockBehavior.Loose);
            AzureUnitTestUtility.ConfigureSuccessfulLease(blob, leaseClient);
            blob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, AzureUnitTestUtility.Response));
            messageBlob.Setup(b => b.UploadAsync(It.IsAny<BinaryData>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue<BlobContentInfo>(null, AzureUnitTestUtility.Response));
            messageBlob.Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, AzureUnitTestUtility.Response));

            var fileLock = new AzureFastFileSystemLock(blob.Object, messageBlob.Object, log);
            (await fileLock.GetLock(TimeSpan.Zero, "holder", TestContext.Current.CancellationToken)).Should().BeTrue();

            fileLock.Dispose();

            leaseClient.Verify(l => l.ReleaseAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()));
            messageBlob.Verify(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task AzureBlobLease_GetLease_ReturnsTrueWhenLeaseIdsMatch()
        {
            var blob = new Mock<BlobClient>(MockBehavior.Loose);
            var leaseClient = new Mock<BlobLeaseClient>(MockBehavior.Loose);
            var leaseId = string.Empty;
            AzureUnitTestUtility.ConfigureLeaseClient(blob, leaseClient, id => leaseId = id);
            leaseClient.Setup(l => l.AcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Response.FromValue(
                    BlobsModelFactory.BlobLease(new ETag("\"etag\""), DateTimeOffset.UtcNow, leaseId),
                    AzureUnitTestUtility.Response));

            using (var lease = new AzureBlobLease(blob.Object))
            {
                (await lease.GetLease()).Should().BeTrue();
            }
        }

        [Fact]
        public async Task AzureBlobLease_GetLease_ReturnsFalseWhenLeaseIdsDoNotMatch()
        {
            var blob = new Mock<BlobClient>(MockBehavior.Loose);
            var leaseClient = new Mock<BlobLeaseClient>(MockBehavior.Loose);
            AzureUnitTestUtility.ConfigureLeaseClient(blob, leaseClient);
            leaseClient.Setup(l => l.AcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(
                    BlobsModelFactory.BlobLease(new ETag("\"etag\""), DateTimeOffset.UtcNow, "another-lease"),
                    AzureUnitTestUtility.Response));

            using (var lease = new AzureBlobLease(blob.Object))
            {
                (await lease.GetLease()).Should().BeFalse();
            }
        }

        [Fact]
        public async Task AzureBlobLease_Renew_AndReleaseUseLeaseClient()
        {
            var blob = new Mock<BlobClient>(MockBehavior.Loose);
            var leaseClient = new Mock<BlobLeaseClient>(MockBehavior.Loose);
            AzureUnitTestUtility.ConfigureSuccessfulLease(blob, leaseClient);

            using (var lease = new AzureBlobLease(blob.Object))
            {
                (await lease.GetLease()).Should().BeTrue();
                await lease.Renew();
                lease.Release();
            }

            leaseClient.Verify(l => l.RenewAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()));
            leaseClient.Verify(l => l.ReleaseAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()));
        }

        private sealed class AzureFastFileSystemLock : AzureFileSystemLock
        {
            public AzureFastFileSystemLock(BlobClient blob, BlobClient messageBlob, NuGet.Common.ILogger log)
                : base(blob, messageBlob, log)
            {
            }

            protected override TimeSpan WaitBetweenAttempts => TimeSpan.FromMilliseconds(1);
        }
    }
}
