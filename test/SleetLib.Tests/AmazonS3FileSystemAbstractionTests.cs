using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3.Model;
using AwesomeAssertions;
using Moq;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class AmazonS3FileSystemAbstractionTests
    {
        [Fact]
        public async Task AmazonS3FileSystemAbstraction_CreateFileAsync_SendsBodyEncryptionAndPayloadSigning()
        {
            var client = AmazonS3TestUtility.CreateClient();
            var captured = (PutObjectRequest)null;
            client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                .Callback<PutObjectRequest, CancellationToken>((r, _) => captured = r)
                .ReturnsAsync(new PutObjectResponse());

            await AmazonS3FileSystemAbstraction.CreateFileAsync(client.Object, AmazonS3TestUtility.BucketName, "key", "body", Amazon.S3.ServerSideEncryptionMethod.AES256, true, TestContext.Current.CancellationToken);

            captured.BucketName.Should().Be(AmazonS3TestUtility.BucketName);
            captured.Key.Should().Be("key");
            captured.ContentBody.Should().Be("body");
            captured.ServerSideEncryptionMethod.Should().Be(Amazon.S3.ServerSideEncryptionMethod.AES256);
            captured.DisablePayloadSigning.Should().BeTrue();
        }

        [Fact]
        public async Task AmazonS3FileSystemAbstraction_DownloadFileAsync_CopiesBodyAndReturnsEncoding()
        {
            var client = AmazonS3TestUtility.CreateClient();
            client.Setup(c => c.GetObjectAsync(AmazonS3TestUtility.BucketName, "key", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetObjectResponse { ResponseStream = new MemoryStream(new byte[] { 1, 2, 3 }), Headers = { ContentEncoding = "gzip" } });
            using (var target = new MemoryStream())
            {
                var encoding = await AmazonS3FileSystemAbstraction.DownloadFileAsync(client.Object, AmazonS3TestUtility.BucketName, "key", target, TestContext.Current.CancellationToken);

                encoding.Should().Be("gzip");
                target.ToArray().Should().Equal(1, 2, 3);
            }
        }

        [Fact]
        public async Task AmazonS3FileSystemAbstraction_GetFilesAsync_FollowsPaging()
        {
            var client = AmazonS3TestUtility.CreateClient();
            var requests = new List<ListObjectsV2Request>();
            var continuationTokens = new List<string>();
            var responses = new Queue<ListObjectsV2Response>(new[]
            {
                new ListObjectsV2Response { IsTruncated = true, NextContinuationToken = "next", S3Objects = new List<S3Object> { new S3Object { Key = "a" } } },
                new ListObjectsV2Response { S3Objects = new List<S3Object> { new S3Object { Key = "b" } } }
            });
            // The same request object is reused for each page, so the continuation token is recorded per call.
            client.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                .Callback<ListObjectsV2Request, CancellationToken>((r, _) =>
                {
                    requests.Add(r);
                    continuationTokens.Add(r.ContinuationToken);
                })
                .ReturnsAsync(() => responses.Dequeue());

            var files = await AmazonS3FileSystemAbstraction.GetFilesAsync(client.Object, AmazonS3TestUtility.BucketName, TestContext.Current.CancellationToken);

            files.Select(e => e.Key).Should().Equal("a", "b");
            requests.Should().HaveCount(2);
            requests[0].MaxKeys.Should().Be(100);
            continuationTokens[1].Should().Be("next");
        }

        [Fact]
        public async Task AmazonS3FileSystemAbstraction_RemoveMultipleFilesAsync_SkipsEmptyBatch()
        {
            var client = AmazonS3TestUtility.CreateClient();

            await AmazonS3FileSystemAbstraction.RemoveMultipleFilesAsync(client.Object, AmazonS3TestUtility.BucketName, Enumerable.Empty<KeyVersion>(), TestContext.Current.CancellationToken);

            client.Verify(c => c.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task AmazonS3FileSystemAbstraction_RemoveMultipleFilesAsync_SendsBatch()
        {
            var client = AmazonS3TestUtility.CreateClient();
            var captured = (DeleteObjectsRequest)null;
            client.Setup(c => c.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(), It.IsAny<CancellationToken>()))
                .Callback<DeleteObjectsRequest, CancellationToken>((r, _) => captured = r)
                .ReturnsAsync(new DeleteObjectsResponse());

            await AmazonS3FileSystemAbstraction.RemoveMultipleFilesAsync(client.Object, AmazonS3TestUtility.BucketName, new[] { new KeyVersion { Key = "a" }, new KeyVersion { Key = "b" } }, TestContext.Current.CancellationToken);

            captured.BucketName.Should().Be(AmazonS3TestUtility.BucketName);
            captured.Objects.Select(e => e.Key).Should().Equal("a", "b");
        }

        [Fact]
        public async Task AmazonS3FileSystemAbstraction_RemoveFileAsync_DeletesObject()
        {
            var client = AmazonS3TestUtility.CreateClient();
            client.Setup(c => c.DeleteObjectAsync(AmazonS3TestUtility.BucketName, "key", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteObjectResponse());

            await AmazonS3FileSystemAbstraction.RemoveFileAsync(client.Object, AmazonS3TestUtility.BucketName, "key", TestContext.Current.CancellationToken);

            client.Verify(c => c.DeleteObjectAsync(AmazonS3TestUtility.BucketName, "key", It.IsAny<CancellationToken>()), Times.Once());
        }
    }
}
