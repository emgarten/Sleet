using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Runtime.SharedInterfaces;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class AmazonS3FileSystemTests
    {
        [Fact]
        public async Task CreateBucket_WithAmazonS3_SetsPublicReadPolicy()
        {
            var (fileSystem, client) = await CreateFileSystemWithFakeClientAsync(source => source["region"] = "us-east-1");

            await fileSystem.CreateBucket(new TestLogger(), CancellationToken.None);

            client.Calls.Should().ContainInOrder("EnsureBucketExistsAsync", "PutPublicAccessBlockAsync", "PutBucketOwnershipControlsAsync", "PutBucketPolicyAsync", "PutObjectAsync");
            client.Calls.Should().NotContain("PutBucketAclAsync");
        }

        [Theory]
        [InlineData(HttpStatusCode.NotImplemented, "NotImplemented")]
        [InlineData(HttpStatusCode.MethodNotAllowed, "MethodNotAllowed")]
        [InlineData(HttpStatusCode.BadRequest, "MalformedXML")]
        public async Task CreateBucket_WhenPublicAccessBlockIsNotSupported_SetsPublicReadPolicy(HttpStatusCode statusCode, string errorCode)
        {
            var (fileSystem, client) = await CreateFileSystemWithFakeClientAsync(source =>
            {
                source["provider"] = "minio";
                source["serviceURL"] = "http://localhost:9000";
            });

            client.Failures["PutPublicAccessBlockAsync"] = CreateException(statusCode, errorCode);
            client.Failures["PutBucketOwnershipControlsAsync"] = CreateException(statusCode, errorCode);

            await fileSystem.CreateBucket(new TestLogger(), CancellationToken.None);

            // Errors that retrying will not fix are not retried
            client.Calls.Count(e => e == "PutPublicAccessBlockAsync").Should().Be(1);
            client.Calls.Count(e => e == "PutBucketOwnershipControlsAsync").Should().Be(1);
            client.Calls.Should().ContainInOrder("PutBucketPolicyAsync", "PutObjectAsync");
        }

        [Fact]
        public async Task CreateBucket_WhenPublicAccessBlockIsForbidden_Throws()
        {
            var (fileSystem, client) = await CreateFileSystemWithFakeClientAsync(source => source["region"] = "us-east-1");
            client.Failures["PutPublicAccessBlockAsync"] = CreateException(HttpStatusCode.Forbidden, "AccessDenied");

            Func<Task> act = () => fileSystem.CreateBucket(new TestLogger(), CancellationToken.None);

            await act.Should().ThrowAsync<AmazonS3Exception>();
            client.Calls.Should().NotContain("PutBucketPolicyAsync");
        }

        [Fact]
        public async Task CreateBucket_WhenBucketPolicyIsNotImplemented_Throws()
        {
            var (fileSystem, client) = await CreateFileSystemWithFakeClientAsync(source => source["region"] = "us-east-1");
            client.Failures["PutBucketPolicyAsync"] = CreateException(HttpStatusCode.NotImplemented, "NotImplemented");

            Func<Task> act = () => fileSystem.CreateBucket(new TestLogger(), CancellationToken.None);

            await act.Should().ThrowAsync<AmazonS3Exception>();
            client.Calls.Count(e => e == "PutBucketPolicyAsync").Should().Be(1);
            client.Calls.Should().NotContain("PutObjectAsync");
        }

        [Fact]
        public async Task CreateBucket_WithCloudflareR2_SkipsPublicAccessSettings()
        {
            var (fileSystem, client) = await CreateFileSystemWithFakeClientAsync(source =>
            {
                source["provider"] = "r2";
                source["serviceURL"] = "https://account.r2.cloudflarestorage.com";
                source["baseURI"] = "https://nuget.example.com/";
                source["acl"] = "public-read";
            });
            var log = new TestLogger();

            await fileSystem.CreateBucket(log, CancellationToken.None);

            client.Calls.Should().ContainInOrder("EnsureBucketExistsAsync", "PutObjectAsync");
            client.Calls.Should().NotContain(new[] { "PutPublicAccessBlockAsync", "PutBucketOwnershipControlsAsync", "PutBucketPolicyAsync", "PutBucketAclAsync" });
            log.GetMessages().Should().Contain("Cloudflare R2 buckets are private by default");
        }

        private static async Task<(AmazonS3FileSystem FileSystem, FakeS3Client Client)> CreateFileSystemWithFakeClientAsync(Action<JObject> configure)
        {
            var fileSystem = await FileSystemFactoryTests.CreateS3FileSystemAsync(configure);
            var client = new FakeS3Client();

            SetPrivateField(fileSystem, "_client", client);

            // Skip the bucket exists check
            SetPrivateField(fileSystem, "_hasBucket", false);

            return (fileSystem, client);
        }

        private static void SetPrivateField(AmazonS3FileSystem fileSystem, string name, object value)
        {
            typeof(AmazonS3FileSystem)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(fileSystem, value);
        }

        private static AmazonS3Exception CreateException(HttpStatusCode statusCode, string errorCode)
        {
            return new AmazonS3Exception($"{errorCode} test error", ErrorType.Sender, errorCode, "requestId", statusCode);
        }

        /// <summary>
        /// Records the calls made when creating a bucket. Calls succeed unless a failure is set for the method name.
        /// </summary>
        private class FakeS3Client : AmazonS3Client, ICoreAmazonS3
        {
            public FakeS3Client()
                : base(new BasicAWSCredentials("key", "secret"), new AmazonS3Config { ServiceURL = "https://s3.example.com" })
            {
            }

            public List<string> Calls { get; } = new List<string>();

            public Dictionary<string, Exception> Failures { get; } = new Dictionary<string, Exception>();

            Task ICoreAmazonS3.EnsureBucketExistsAsync(string bucketName) => Record<object>("EnsureBucketExistsAsync", null);

            public override Task<PutPublicAccessBlockResponse> PutPublicAccessBlockAsync(PutPublicAccessBlockRequest request, CancellationToken cancellationToken = default)
                => Record(nameof(PutPublicAccessBlockAsync), new PutPublicAccessBlockResponse());

            public override Task<PutBucketOwnershipControlsResponse> PutBucketOwnershipControlsAsync(PutBucketOwnershipControlsRequest request, CancellationToken cancellationToken = default)
                => Record(nameof(PutBucketOwnershipControlsAsync), new PutBucketOwnershipControlsResponse());

            public override Task<PutBucketPolicyResponse> PutBucketPolicyAsync(PutBucketPolicyRequest request, CancellationToken cancellationToken = default)
                => Record(nameof(PutBucketPolicyAsync), new PutBucketPolicyResponse());

            public override Task<PutBucketAclResponse> PutBucketAclAsync(PutBucketAclRequest request, CancellationToken cancellationToken = default)
                => Record(nameof(PutBucketAclAsync), new PutBucketAclResponse());

            public override Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken cancellationToken = default)
                => Record(nameof(ListObjectsV2Async), new ListObjectsV2Response());

            public override Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
                => Record(nameof(PutObjectAsync), new PutObjectResponse());

            private Task<T> Record<T>(string name, T response)
            {
                Calls.Add(name);

                return Failures.TryGetValue(name, out var failure)
                    ? Task.FromException<T>(failure)
                    : Task.FromResult(response);
            }
        }
    }
}
