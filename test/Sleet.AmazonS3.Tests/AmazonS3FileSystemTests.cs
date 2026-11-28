using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3.Model;
using AwesomeAssertions;
using NuGet.Test.Helpers;
using Xunit;

namespace Sleet.AmazonS3.Tests
{
    public class AmazonS3FileSystemTests
    {
        [AmazonS3Fact]
        public async Task GivenAS3AccountVerifyBucketOperations()
        {
            await using (var testContext = new AmazonS3TestContext())
            {
                testContext.CreateBucketOnInit = false;
                await testContext.InitAsync();

                // Verify at the start
                (await testContext.FileSystem.HasBucket(testContext.Logger, CancellationToken.None)).Should().BeFalse("HasBucket should return false since it does not exist yet");
                (await testContext.FileSystem.Validate(testContext.Logger, CancellationToken.None)).Should().BeFalse("Validate should be false since the bucket does not exist yet");

                // Create
                await testContext.FileSystem.CreateBucket(testContext.Logger, CancellationToken.None);

                var hasBucket = (await testContext.FileSystem.HasBucket(testContext.Logger, CancellationToken.None));
                hasBucket.Should().BeTrue("HasBucket should return true after");
                var valid = (await testContext.FileSystem.Validate(testContext.Logger, CancellationToken.None));
                valid.Should().BeTrue("Validate should be true after");

                // Delete
                await testContext.FileSystem.DeleteBucket(testContext.Logger, CancellationToken.None);

                (await testContext.FileSystem.HasBucket(testContext.Logger, CancellationToken.None)).Should().BeFalse();
                (await testContext.FileSystem.Validate(testContext.Logger, CancellationToken.None)).Should().BeFalse();

                await testContext.CleanupAsync();
            }
        }

        [AmazonS3Fact]
        public async Task GivenABucketCreatedAfterSleetCheckedForItVerifyCreateBucketKeepsItPrivate()
        {
            Assert.SkipWhen(AmazonS3TestContext.IsCloudflareR2, "Sleet doesn't change the access settings of Cloudflare R2 buckets, and the R2 S3 API never allows anonymous reads.");

            var token = TestContext.Current.CancellationToken;

            await using (var testContext = new AmazonS3TestContext())
            {
                testContext.CreateBucketOnInit = false;
                await testContext.InitAsync();

                (await testContext.FileSystem.HasBucket(testContext.Logger, token)).Should().BeFalse();

                // Something else creates a private bucket with the same name before Sleet creates it
                await testContext.CreateBucketAsync(token);
                await testContext.Client.PutObjectAsync(new PutObjectRequest()
                {
                    BucketName = testContext.BucketName,
                    Key = "private.txt",
                    ContentBody = "private"
                }, token);

                await testContext.FileSystem.CreateBucket(testContext.Logger, token);

                (await testContext.FileSystem.HasBucket(testContext.Logger, token)).Should().BeTrue();

                using (var httpClient = new HttpClient())
                using (var response = await httpClient.GetAsync(new Uri(testContext.Uri, "private.txt"), token))
                {
                    response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Sleet must not make a bucket that it didn't create public");
                }

                await testContext.CleanupAsync();
            }
        }
    }
}