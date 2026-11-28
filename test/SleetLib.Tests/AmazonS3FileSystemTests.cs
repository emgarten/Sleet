using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Moq;
using NuGet.Common;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class AmazonS3FileSystemTests
    {
        [Theory]
        [InlineData(null, "a.json", "a.json")]
        [InlineData("/feed/sub/", "a.json", "feed/sub/a.json")]
        public void AmazonS3FileSystem_GetRelativePath_AddsNormalizedFeedSubPath(string feedSubPath, string path, string expected)
        {
            using (var cache = new LocalCache())
            {
                var root = AmazonS3TestUtility.RootUri();
                var fileSystem = new AmazonS3FileSystem(cache, root, root, AmazonS3TestUtility.CreateClient().Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None, feedSubPath: feedSubPath);

                fileSystem.GetRelativePath(fileSystem.GetPath(path)).Should().Be(expected);
            }
        }

        [Fact]
        public void AmazonS3FileSystem_Get_MapsDisplayUriToS3KeyWithFeedSubPath()
        {
            using (var cache = new LocalCache())
            {
                var client = AmazonS3TestUtility.CreateClient();
                var root = AmazonS3TestUtility.RootUri();
                var baseUri = new Uri("https://example.test/packages/");
                var fileSystem = new AmazonS3FileSystem(cache, root, baseUri, client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None, feedSubPath: "v3");

                var file = fileSystem.Get(new Uri("https://example.test/packages/index.json"));

                file.RootPath.AbsoluteUri.Should().Be("https://test-bucket.s3.amazonaws.com/index.json");
                file.EntityUri.AbsoluteUri.Should().Be("https://example.test/packages/index.json");
                fileSystem.GetRelativePath(file.RootPath).Should().Be("v3/index.json");
            }
        }

        [Fact]
        public async Task AmazonS3FileSystem_GetFiles_FiltersSubPathStripsItSkipsLockAndFollowsPaging()
        {
            using (var cache = new LocalCache())
            {
                var client = AmazonS3TestUtility.CreateClient();
                var requests = new List<ListObjectsV2Request>();
                var continuationTokens = new List<string>();
                var responses = new Queue<ListObjectsV2Response>(new[]
                {
                    new ListObjectsV2Response
                    {
                        IsTruncated = true,
                        NextContinuationToken = "next",
                        S3Objects = new List<S3Object>
                        {
                            new S3Object { Key = "feed/a.json" },
                            new S3Object { Key = "other/b.json" }
                        }
                    },
                    new ListObjectsV2Response
                    {
                        S3Objects = new List<S3Object>
                        {
                            new S3Object { Key = "feed/.feedlock" },
                            new S3Object { Key = "feed/c.json" }
                        }
                    }
                });
                // The same request object is reused for each page, so the continuation token is recorded per call.
                client.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                    .Callback<ListObjectsV2Request, CancellationToken>((r, _) =>
                    {
                        requests.Add(r);
                        continuationTokens.Add(r.ContinuationToken);
                    })
                    .ReturnsAsync(() => responses.Dequeue());
                var root = AmazonS3TestUtility.RootUri();
                var fileSystem = new AmazonS3FileSystem(cache, root, root, client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None, feedSubPath: "feed");

                var files = await fileSystem.GetFiles(new TestLogger(), TestContext.Current.CancellationToken);

                files.Select(e => e.EntityUri.AbsoluteUri).Should().BeEquivalentTo(new[]
                {
                    "https://test-bucket.s3.amazonaws.com/a.json",
                    "https://test-bucket.s3.amazonaws.com/c.json"
                });
                requests.Should().HaveCount(2);
                requests[0].BucketName.Should().Be(AmazonS3TestUtility.BucketName);
                requests[0].MaxKeys.Should().Be(100);
                continuationTokens[1].Should().Be("next");
            }
        }

        [Fact]
        public async Task AmazonS3FileSystem_Validate_LogsErrorWhenBucketMissingAndCachesResult()
        {
            using (var cache = new LocalCache())
            {
                var client = AmazonS3TestUtility.CreateClient();
                client.Setup(c => c.HeadBucketAsync(It.IsAny<HeadBucketRequest>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(AmazonS3TestUtility.S3Exception(HttpStatusCode.NotFound));
                var fileSystem = new AmazonS3FileSystem(cache, AmazonS3TestUtility.RootUri(), AmazonS3TestUtility.RootUri(), client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None);
                var log = new TestLogger();

                var first = await fileSystem.Validate(log, TestContext.Current.CancellationToken);
                var second = await fileSystem.HasBucket(log, TestContext.Current.CancellationToken);

                first.Should().BeFalse();
                second.Should().BeFalse();
                log.GetMessages().Should().Contain("Unable to find test-bucket");
                client.Verify(
                    c => c.HeadBucketAsync(It.Is<HeadBucketRequest>(r => r.BucketName == AmazonS3TestUtility.BucketName), It.IsAny<CancellationToken>()),
                    Times.Once());
            }
        }

        [Theory]
        [InlineData(HttpStatusCode.Forbidden, true)]
        [InlineData(HttpStatusCode.MovedPermanently, true)]
        [InlineData(HttpStatusCode.NotFound, false)]
        public async Task AmazonS3FileSystem_HasBucket_MapsHeadBucketErrors(HttpStatusCode statusCode, bool expected)
        {
            using (var cache = new LocalCache())
            {
                var client = AmazonS3TestUtility.CreateClient();
                client.Setup(c => c.HeadBucketAsync(It.IsAny<HeadBucketRequest>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(AmazonS3TestUtility.S3Exception(statusCode));
                var fileSystem = new AmazonS3FileSystem(cache, AmazonS3TestUtility.RootUri(), AmazonS3TestUtility.RootUri(), client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None);

                var exists = await fileSystem.HasBucket(new TestLogger(), TestContext.Current.CancellationToken);

                exists.Should().Be(expected);
            }
        }

        [Fact]
        public async Task AmazonS3FileSystem_HasBucket_ThrowsOtherErrorsAndDoesNotCacheThem()
        {
            using (var cache = new LocalCache())
            {
                var client = AmazonS3TestUtility.CreateClient();
                client.SetupSequence(c => c.HeadBucketAsync(It.IsAny<HeadBucketRequest>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(AmazonS3TestUtility.S3Exception(HttpStatusCode.InternalServerError))
                    .ReturnsAsync(new HeadBucketResponse());
                var fileSystem = new AmazonS3FileSystem(cache, AmazonS3TestUtility.RootUri(), AmazonS3TestUtility.RootUri(), client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None);
                var log = new TestLogger();

                var exception = await Assert.ThrowsAsync<AmazonS3Exception>(async () => await fileSystem.HasBucket(log, TestContext.Current.CancellationToken));
                var exists = await fileSystem.HasBucket(log, TestContext.Current.CancellationToken);

                exception.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                exists.Should().BeTrue();
            }
        }

        [Fact]
        public async Task AmazonS3FileSystem_CreateBucket_CallsSetupAclAndCreateReleaseLock()
        {
            using (var cache = new LocalCache())
            {
                var client = CreateClientWithMissingBucket();
                client.Setup(c => c.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutBucketResponse());
                client.Setup(c => c.PutPublicAccessBlockAsync(It.IsAny<PutPublicAccessBlockRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutPublicAccessBlockResponse());
                client.Setup(c => c.PutBucketOwnershipControlsAsync(It.IsAny<PutBucketOwnershipControlsRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutBucketOwnershipControlsResponse());
                client.Setup(c => c.PutBucketPolicyAsync(It.IsAny<PutBucketPolicyRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutBucketPolicyResponse());
                client.Setup(c => c.PutBucketAclAsync(It.IsAny<PutBucketAclRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutBucketAclResponse());
                client.SetupSequence(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ListObjectsV2Response())
                    .ReturnsAsync(new ListObjectsV2Response { S3Objects = new List<S3Object> { new S3Object { Key = AmazonS3FileSystemLock.LockFile } } });
                client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutObjectResponse());
                var fileSystem = new AmazonS3FileSystem(cache, AmazonS3TestUtility.RootUri(), AmazonS3TestUtility.RootUri(), client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.AES256, acl: S3CannedACL.PublicRead, disablePayloadSigning: true);

                await fileSystem.CreateBucket(new TestLogger(), TestContext.Current.CancellationToken);

                // Amazon S3 creates the bucket in the region of the client
                client.Verify(c => c.PutBucketAsync(It.Is<PutBucketRequest>(r => r.BucketName == AmazonS3TestUtility.BucketName && r.UseClientRegion), It.IsAny<CancellationToken>()), Times.Once());
                client.Verify(c => c.PutPublicAccessBlockAsync(It.Is<PutPublicAccessBlockRequest>(r => r.BucketName == AmazonS3TestUtility.BucketName && r.PublicAccessBlockConfiguration.BlockPublicAcls == false), It.IsAny<CancellationToken>()), Times.Once());
                client.Verify(c => c.PutBucketOwnershipControlsAsync(It.Is<PutBucketOwnershipControlsRequest>(r => r.OwnershipControls.Rules[0].ObjectOwnership == ObjectOwnership.BucketOwnerPreferred), It.IsAny<CancellationToken>()), Times.Once());
                client.Verify(c => c.PutBucketPolicyAsync(It.Is<PutBucketPolicyRequest>(r => r.Policy.Contains("arn:aws:s3:::test-bucket/*")), It.IsAny<CancellationToken>()), Times.Once());
                client.Verify(c => c.PutBucketAclAsync(It.Is<PutBucketAclRequest>(r => r.ACL == S3CannedACL.PublicRead), It.IsAny<CancellationToken>()), Times.Once());
                client.Verify(c => c.PutObjectAsync(It.Is<PutObjectRequest>(r => r.Key == AmazonS3FileSystemLock.LockFile && r.ServerSideEncryptionMethod == ServerSideEncryptionMethod.AES256 && r.DisablePayloadSigning == true), It.IsAny<CancellationToken>()), Times.Once());
                client.Verify(c => c.DeleteObjectAsync(AmazonS3TestUtility.BucketName, AmazonS3FileSystemLock.LockFile, It.IsAny<CancellationToken>()), Times.Once());
            }
        }

        [Fact]
        public async Task AmazonS3FileSystem_CreateBucket_DoesNotSetAclWhenAclIsNull()
        {
            using (var cache = new LocalCache())
            {
                var client = CreateClientWithMissingBucket();
                client.Setup(c => c.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutBucketResponse());
                client.Setup(c => c.PutPublicAccessBlockAsync(It.IsAny<PutPublicAccessBlockRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutPublicAccessBlockResponse());
                client.Setup(c => c.PutBucketOwnershipControlsAsync(It.IsAny<PutBucketOwnershipControlsRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutBucketOwnershipControlsResponse());
                client.Setup(c => c.PutBucketPolicyAsync(It.IsAny<PutBucketPolicyRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutBucketPolicyResponse());
                client.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ListObjectsV2Response());
                client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutObjectResponse());
                var fileSystem = new AmazonS3FileSystem(cache, AmazonS3TestUtility.RootUri(), AmazonS3TestUtility.RootUri(), client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None);

                await fileSystem.CreateBucket(new TestLogger(), TestContext.Current.CancellationToken);

                client.Verify(c => c.PutBucketPolicyAsync(It.IsAny<PutBucketPolicyRequest>(), It.IsAny<CancellationToken>()), Times.Once());
                client.Verify(c => c.PutBucketAclAsync(It.IsAny<PutBucketAclRequest>(), It.IsAny<CancellationToken>()), Times.Never());
            }
        }

        // Something else created the bucket between the check and the create request, it may be a private bucket
        [Theory]
        [InlineData("aws", "BucketAlreadyOwnedByYou")]
        [InlineData("aws", "OperationAborted")]
        [InlineData("self-hosted", "BucketAlreadyExists")]
        public async Task AmazonS3FileSystem_CreateBucket_WhenTheCreateRequestConflicts_DoesNotChangeAccessSettings(string provider, string errorCode)
        {
            var fileSystem = await FileSystemFactoryTests.CreateS3FileSystemAsync(source =>
            {
                source["provider"] = provider;
                source["acl"] = "public-read";

                if (provider == "aws")
                {
                    source["region"] = "us-west-2";
                }
                else
                {
                    source["serviceURL"] = "https://s3.example.com";
                }
            });
            var client = CreateClientWithMissingBucket();
            client.Setup(c => c.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(CreateBucketConflict(errorCode));
            client.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ListObjectsV2Response());
            client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutObjectResponse());
            SetClient(fileSystem, client.Object);
            var log = new TestLogger();

            await fileSystem.CreateBucket(log, TestContext.Current.CancellationToken);

            VerifyAccessSettingsNotChanged(client);
            client.Verify(c => c.PutObjectAsync(It.Is<PutObjectRequest>(r => r.Key == AmazonS3FileSystemLock.LockFile), It.IsAny<CancellationToken>()), Times.Once());
            log.GetMessages().Should().Contain($"Sleet didn't change its access settings. For a public feed, allow public read access to the bucket. {errorCode}:");
            (await fileSystem.HasBucket(log, TestContext.Current.CancellationToken)).Should().BeTrue();
        }

        [Fact]
        public async Task AmazonS3FileSystem_CreateBucket_WhenAnotherAwsAccountOwnsTheBucket_ThrowsWithoutChangingAccessSettings()
        {
            using (var cache = new LocalCache())
            {
                var client = CreateClientWithMissingBucket();
                client.Setup(c => c.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(CreateBucketConflict("BucketAlreadyExists"));
                var fileSystem = new AmazonS3FileSystem(cache, AmazonS3TestUtility.RootUri(), AmazonS3TestUtility.RootUri(), client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None, acl: S3CannedACL.PublicRead);

                var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await fileSystem.CreateBucket(new TestLogger(), TestContext.Current.CancellationToken));

                exception.Message.Should().Contain("the bucket name is already taken").And.Contain("BucketAlreadyExists");
                exception.InnerException.Should().BeOfType<BucketAlreadyExistsException>();
                VerifyAccessSettingsNotChanged(client);
            }
        }

        [Fact]
        public async Task AmazonS3FileSystem_CreateBucket_WhenTheBucketIsCreatedAfterHasBucket_DoesNotCreateOrChangeIt()
        {
            using (var cache = new LocalCache())
            {
                var client = AmazonS3TestUtility.CreateClient();
                client.SetupSequence(c => c.HeadBucketAsync(It.IsAny<HeadBucketRequest>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(AmazonS3TestUtility.S3Exception(HttpStatusCode.NotFound))
                    .ReturnsAsync(new HeadBucketResponse());
                var fileSystem = new AmazonS3FileSystem(cache, AmazonS3TestUtility.RootUri(), AmazonS3TestUtility.RootUri(), client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None);
                var log = new TestLogger();

                var existsBefore = await fileSystem.HasBucket(log, TestContext.Current.CancellationToken);
                await fileSystem.CreateBucket(log, TestContext.Current.CancellationToken);
                var existsAfter = await fileSystem.HasBucket(log, TestContext.Current.CancellationToken);

                existsBefore.Should().BeFalse();
                existsAfter.Should().BeTrue();
                client.Verify(c => c.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>()), Times.Never());
                VerifyAccessSettingsNotChanged(client);
            }
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        public async Task AmazonS3FileSystem_CreateBucket_DoesNotRetryUnauthorized(HttpStatusCode statusCode)
        {
            using (var cache = new LocalCache())
            {
                var client = CreateClientForRetryTest(statusCode, false);
                var fileSystem = new AmazonS3FileSystem(cache, AmazonS3TestUtility.RootUri(), AmazonS3TestUtility.RootUri(), client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None);

                var exception = await Assert.ThrowsAsync<AmazonS3Exception>(async () => await fileSystem.CreateBucket(new TestLogger(), TestContext.Current.CancellationToken));

                exception.StatusCode.Should().Be(statusCode);
                client.Verify(c => c.PutPublicAccessBlockAsync(It.IsAny<PutPublicAccessBlockRequest>(), It.IsAny<CancellationToken>()), Times.Once());
            }
        }

        [Fact]
        public async Task AmazonS3FileSystem_CreateBucket_ForbiddenLogsWarningsAndDoesNotRetry()
        {
            using (var cache = new LocalCache())
            {
                var client = CreateClientForRetryTest(HttpStatusCode.Forbidden, false);
                var log = new TestLogger();
                var fileSystem = new AmazonS3FileSystem(cache, AmazonS3TestUtility.RootUri(), AmazonS3TestUtility.RootUri(), client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None);

                await Assert.ThrowsAsync<AmazonS3Exception>(async () => await fileSystem.CreateBucket(log, TestContext.Current.CancellationToken));

                log.GetMessages().Should().Contain("AmazonS3FullAccess");
                client.Verify(c => c.PutPublicAccessBlockAsync(It.IsAny<PutPublicAccessBlockRequest>(), It.IsAny<CancellationToken>()), Times.Once());
            }
        }

        [Fact]
        public async Task AmazonS3FileSystem_CreateBucket_RetriesOtherStatusCodesOnce()
        {
            using (var cache = new LocalCache())
            {
                var client = CreateClientForRetryTest(HttpStatusCode.InternalServerError, true);
                var log = new TestLogger();
                var fileSystem = new AmazonS3FileSystem(cache, AmazonS3TestUtility.RootUri(), AmazonS3TestUtility.RootUri(), client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None);

                await fileSystem.CreateBucket(log, TestContext.Current.CancellationToken);

                log.GetMessages().Should().Contain("Trying again");
                client.Verify(c => c.PutPublicAccessBlockAsync(It.IsAny<PutPublicAccessBlockRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            }
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)] // MinIO returns MalformedXML
        [InlineData(HttpStatusCode.MethodNotAllowed)]
        [InlineData(HttpStatusCode.NotImplemented)]
        public async Task AmazonS3FileSystem_CreateBucket_SkipsUnsupportedPublicAccessSettingsWithoutRetrying(HttpStatusCode statusCode)
        {
            using (var cache = new LocalCache())
            {
                var client = CreateClientForRetryTest(statusCode, false);
                client.Setup(c => c.PutBucketOwnershipControlsAsync(It.IsAny<PutBucketOwnershipControlsRequest>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(AmazonS3TestUtility.S3Exception(statusCode));
                var fileSystem = new AmazonS3FileSystem(cache, AmazonS3TestUtility.RootUri(), AmazonS3TestUtility.RootUri(), client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None);

                await fileSystem.CreateBucket(new TestLogger(), TestContext.Current.CancellationToken);

                client.Verify(c => c.PutPublicAccessBlockAsync(It.IsAny<PutPublicAccessBlockRequest>(), It.IsAny<CancellationToken>()), Times.Once());
                client.Verify(c => c.PutBucketOwnershipControlsAsync(It.IsAny<PutBucketOwnershipControlsRequest>(), It.IsAny<CancellationToken>()), Times.Once());
                client.Verify(c => c.PutBucketPolicyAsync(It.IsAny<PutBucketPolicyRequest>(), It.IsAny<CancellationToken>()), Times.Once());
            }
        }

        [Fact]
        public async Task AmazonS3FileSystem_CreateBucket_WhenBucketPolicyIsNotSupported_ThrowsWithoutRetrying()
        {
            using (var cache = new LocalCache())
            {
                var client = CreateClientForRetryTest(HttpStatusCode.NotImplemented, false);
                client.Setup(c => c.PutBucketPolicyAsync(It.IsAny<PutBucketPolicyRequest>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(AmazonS3TestUtility.S3Exception(HttpStatusCode.NotImplemented));
                var fileSystem = new AmazonS3FileSystem(cache, AmazonS3TestUtility.RootUri(), AmazonS3TestUtility.RootUri(), client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None);

                var exception = await Assert.ThrowsAsync<AmazonS3Exception>(async () => await fileSystem.CreateBucket(new TestLogger(), TestContext.Current.CancellationToken));

                exception.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
                client.Verify(c => c.PutBucketPolicyAsync(It.IsAny<PutBucketPolicyRequest>(), It.IsAny<CancellationToken>()), Times.Once());
            }
        }

        [Fact]
        public async Task AmazonS3FileSystem_CreateBucket_WithCloudflareR2_SkipsPublicAccessSettings()
        {
            var fileSystem = await FileSystemFactoryTests.CreateS3FileSystemAsync(source =>
            {
                source["provider"] = "r2";
                source["serviceURL"] = "https://account.r2.cloudflarestorage.com";
                source["baseURI"] = "https://nuget.example.com/";
                source["acl"] = "public-read";
            });
            var client = CreateClientWithMissingBucket();
            client.Setup(c => c.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutBucketResponse());
            client.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ListObjectsV2Response());
            client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutObjectResponse());
            SetClient(fileSystem, client.Object);
            var log = new TestLogger();

            await fileSystem.CreateBucket(log, TestContext.Current.CancellationToken);

            client.Verify(c => c.PutBucketAsync(It.Is<PutBucketRequest>(r => r.BucketName == AmazonS3TestUtility.BucketName), It.IsAny<CancellationToken>()), Times.Once());
            client.Verify(c => c.PutPublicAccessBlockAsync(It.IsAny<PutPublicAccessBlockRequest>(), It.IsAny<CancellationToken>()), Times.Never());
            client.Verify(c => c.PutBucketOwnershipControlsAsync(It.IsAny<PutBucketOwnershipControlsRequest>(), It.IsAny<CancellationToken>()), Times.Never());
            client.Verify(c => c.PutBucketPolicyAsync(It.IsAny<PutBucketPolicyRequest>(), It.IsAny<CancellationToken>()), Times.Never());
            client.Verify(c => c.PutBucketAclAsync(It.IsAny<PutBucketAclRequest>(), It.IsAny<CancellationToken>()), Times.Never());
            log.GetMessages().Should().Contain("Cloudflare R2 buckets are private by default");
        }

        [Theory]
        [InlineData("r2")]
        [InlineData("self-hosted")]
        public async Task AmazonS3FileSystem_CreateBucket_WithS3CompatibleProvider_DoesNotUseClientRegion(string provider)
        {
            var fileSystem = await FileSystemFactoryTests.CreateS3FileSystemAsync(source =>
            {
                source["provider"] = provider;
                source["serviceURL"] = "https://s3.example.com";
                source["baseURI"] = "https://nuget.example.com/";
            });
            var client = CreateClientWithMissingBucket();
            client.Setup(c => c.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutBucketResponse());
            client.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ListObjectsV2Response());
            client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutObjectResponse());
            SetClient(fileSystem, client.Object);

            await fileSystem.CreateBucket(new TestLogger(), TestContext.Current.CancellationToken);

            // With the client region the SDK guesses a LocationConstraint from the bucket url, such as ca-4247-9480 from a guid
            client.Verify(c => c.PutBucketAsync(It.Is<PutBucketRequest>(r => r.BucketName == AmazonS3TestUtility.BucketName && !r.UseClientRegion), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task AmazonS3FileSystem_DeleteBucket_IgnoresNotFoundAndClearsCachedHasBucket()
        {
            using (var cache = new LocalCache())
            {
                var client = AmazonS3TestUtility.CreateClient();
                client.Setup(c => c.HeadBucketAsync(It.IsAny<HeadBucketRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HeadBucketResponse());
                client.Setup(c => c.DeleteBucketAsync(AmazonS3TestUtility.BucketName, It.IsAny<CancellationToken>()))
                    .ThrowsAsync(AmazonS3TestUtility.S3Exception(HttpStatusCode.NotFound));
                var fileSystem = new AmazonS3FileSystem(cache, AmazonS3TestUtility.RootUri(), AmazonS3TestUtility.RootUri(), client.Object, AmazonS3TestUtility.BucketName, ServerSideEncryptionMethod.None);
                var log = new TestLogger();

                await fileSystem.DeleteBucket(log, TestContext.Current.CancellationToken);
                var exists = await fileSystem.HasBucket(log, TestContext.Current.CancellationToken);

                exists.Should().BeFalse();
                log.GetMessages().Should().Contain("does not exist any more");
                client.Verify(c => c.HeadBucketAsync(It.IsAny<HeadBucketRequest>(), It.IsAny<CancellationToken>()), Times.Once());
            }
        }

        private static Mock<IAmazonS3> CreateClientWithMissingBucket()
        {
            var client = AmazonS3TestUtility.CreateClient();
            client.Setup(c => c.HeadBucketAsync(It.IsAny<HeadBucketRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(AmazonS3TestUtility.S3Exception(HttpStatusCode.NotFound));
            return client;
        }

        // The error the SDK throws when CreateBucket returns 409 Conflict with this error code
        private static AmazonS3Exception CreateBucketConflict(string errorCode)
        {
            var exception = errorCode switch
            {
                "BucketAlreadyOwnedByYou" => new BucketAlreadyOwnedByYouException("Your previous request to create the named bucket succeeded and you already own it."),
                "BucketAlreadyExists" => new BucketAlreadyExistsException("The requested bucket name is not available."),
                _ => new AmazonS3Exception("A conflicting conditional operation is currently in progress against this resource.")
            };

            exception.StatusCode = HttpStatusCode.Conflict;
            exception.ErrorCode = errorCode;
            return exception;
        }

        private static void VerifyAccessSettingsNotChanged(Mock<IAmazonS3> client)
        {
            client.Verify(c => c.PutPublicAccessBlockAsync(It.IsAny<PutPublicAccessBlockRequest>(), It.IsAny<CancellationToken>()), Times.Never());
            client.Verify(c => c.PutBucketOwnershipControlsAsync(It.IsAny<PutBucketOwnershipControlsRequest>(), It.IsAny<CancellationToken>()), Times.Never());
            client.Verify(c => c.PutBucketPolicyAsync(It.IsAny<PutBucketPolicyRequest>(), It.IsAny<CancellationToken>()), Times.Never());
            client.Verify(c => c.PutBucketAclAsync(It.IsAny<PutBucketAclRequest>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        private static Mock<IAmazonS3> CreateClientForRetryTest(HttpStatusCode statusCode, bool succeedsOnSecondTry)
        {
            var client = CreateClientWithMissingBucket();
            client.Setup(c => c.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutBucketResponse());
            if (succeedsOnSecondTry)
            {
                client.SetupSequence(c => c.PutPublicAccessBlockAsync(It.IsAny<PutPublicAccessBlockRequest>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(AmazonS3TestUtility.S3Exception(statusCode))
                    .ReturnsAsync(new PutPublicAccessBlockResponse());
            }
            else
            {
                client.Setup(c => c.PutPublicAccessBlockAsync(It.IsAny<PutPublicAccessBlockRequest>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(AmazonS3TestUtility.S3Exception(statusCode));
            }

            client.Setup(c => c.PutBucketOwnershipControlsAsync(It.IsAny<PutBucketOwnershipControlsRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutBucketOwnershipControlsResponse());
            client.Setup(c => c.PutBucketPolicyAsync(It.IsAny<PutBucketPolicyRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutBucketPolicyResponse());
            client.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ListObjectsV2Response());
            client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutObjectResponse());
            return client;
        }

        // The provider is internal and only set from sleet.json, replace the client created by the factory
        private static void SetClient(AmazonS3FileSystem fileSystem, IAmazonS3 client)
        {
            typeof(AmazonS3FileSystem).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(fileSystem, client);
        }
    }
}
