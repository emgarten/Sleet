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
    public class AmazonS3FileSystemLockTests
    {
        [Fact]
        public async Task AmazonS3FileSystemLock_GetLock_WhenAbsentCreatesLockWithMessageEncryptionAndPayloadSigning()
        {
            var client = AmazonS3TestUtility.CreateClient();
            var captured = (PutObjectRequest)null;
            client.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ListObjectsV2Response());
            client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                .Callback<PutObjectRequest, CancellationToken>((r, _) => captured = r)
                .ReturnsAsync(new PutObjectResponse());
            var log = new TestLogger();
            using (var feedLock = new AmazonS3TestLock(client.Object, ServerSideEncryptionMethod.AES256, true, log))
            {
                var result = await feedLock.GetLock(TimeSpan.FromMilliseconds(20), "holder", TestContext.Current.CancellationToken);

                result.Should().BeTrue();
                feedLock.IsLocked.Should().BeTrue();
                captured.BucketName.Should().Be(AmazonS3TestUtility.BucketName);
                captured.Key.Should().Be(AmazonS3FileSystemLock.LockFile);
                captured.ServerSideEncryptionMethod.Should().Be(ServerSideEncryptionMethod.AES256);
                captured.DisablePayloadSigning.Should().BeTrue();
                var json = JObject.Parse(captured.ContentBody);
                json["message"].ToString().Should().Be("holder");
                json["date"].ToString().Should().NotBeNullOrEmpty();
            }
        }

        [Fact]
        public async Task AmazonS3FileSystemLock_GetLock_WhenPresentReturnsFalseAndLogsHolderMessage()
        {
            var client = AmazonS3TestUtility.CreateClient();
            client.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ListObjectsV2Response { S3Objects = new List<S3Object> { new S3Object { Key = AmazonS3FileSystemLock.LockFile } } });
            var existing = "{\"message\":\"other holder\",\"date\":\"2026-01-02T03:04:05Z\"}";
            client.Setup(c => c.GetObjectAsync(AmazonS3TestUtility.BucketName, AmazonS3FileSystemLock.LockFile, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetObjectResponse { ResponseStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(existing)) });
            var log = new TestLogger();
            using (var feedLock = new AmazonS3TestLock(client.Object, ServerSideEncryptionMethod.None, false, log))
            {
                var result = await feedLock.GetLock(TimeSpan.Zero, "holder", TestContext.Current.CancellationToken);

                result.Should().BeFalse();
                feedLock.IsLocked.Should().BeFalse();
                log.GetMessages().Should().Contain("Feed is locked by: other holder since: 2026-01-02T03:04:05Z");
            }
        }

        [Fact]
        public async Task AmazonS3FileSystemLock_ReleaseAsync_DeletesLockObjectWhenLocked()
        {
            var client = AmazonS3TestUtility.CreateClient();
            client.SetupSequence(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ListObjectsV2Response())
                .ReturnsAsync(new ListObjectsV2Response { S3Objects = new List<S3Object> { new S3Object { Key = AmazonS3FileSystemLock.LockFile } } });
            client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PutObjectResponse());
            client.Setup(c => c.DeleteObjectAsync(AmazonS3TestUtility.BucketName, AmazonS3FileSystemLock.LockFile, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteObjectResponse());
            var log = new TestLogger();
            using (var feedLock = new AmazonS3TestLock(client.Object, ServerSideEncryptionMethod.None, false, log))
            {
                (await feedLock.GetLock(TimeSpan.FromMilliseconds(20), "holder", TestContext.Current.CancellationToken)).Should().BeTrue();

                await feedLock.ReleaseAsync(TestContext.Current.CancellationToken);

                feedLock.IsLocked.Should().BeFalse();
                client.Verify(
                    c => c.DeleteObjectAsync(AmazonS3TestUtility.BucketName, AmazonS3FileSystemLock.LockFile, It.IsAny<CancellationToken>()),
                    Times.Once());
            }
        }

        [Fact]
        public async Task AmazonS3FileSystemLock_Release_DeletesLockObjectWhenLocked()
        {
            var client = AmazonS3TestUtility.CreateClient();
            client.SetupSequence(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ListObjectsV2Response())
                .ReturnsAsync(new ListObjectsV2Response { S3Objects = new List<S3Object> { new S3Object { Key = AmazonS3FileSystemLock.LockFile } } });
            client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PutObjectResponse());
            client.Setup(c => c.DeleteObjectAsync(AmazonS3TestUtility.BucketName, AmazonS3FileSystemLock.LockFile, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteObjectResponse());
            var log = new TestLogger();
            using (var feedLock = new AmazonS3TestLock(client.Object, ServerSideEncryptionMethod.None, false, log))
            {
                (await feedLock.GetLock(TimeSpan.FromMilliseconds(20), "holder", TestContext.Current.CancellationToken)).Should().BeTrue();

                feedLock.Release();

                feedLock.IsLocked.Should().BeFalse();
                client.Verify(
                    c => c.DeleteObjectAsync(AmazonS3TestUtility.BucketName, AmazonS3FileSystemLock.LockFile, It.IsAny<CancellationToken>()),
                    Times.Once());
            }
        }

        private sealed class AmazonS3TestLock : AmazonS3FileSystemLock
        {
            public AmazonS3TestLock(IAmazonS3 client, ServerSideEncryptionMethod serverSideEncryptionMethod, bool disablePayloadSigning, ILogger log)
                : base(client, AmazonS3TestUtility.BucketName, serverSideEncryptionMethod, disablePayloadSigning, log)
            {
            }

            protected override TimeSpan WaitBetweenAttempts => TimeSpan.FromMilliseconds(1);
        }
    }
}
