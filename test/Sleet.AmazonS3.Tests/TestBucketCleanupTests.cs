using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace Sleet.AmazonS3.Tests
{
    public class TestBucketCleanupTests
    {
        // A run that's stopped before it finishes, such as a cancelled CI run, leaves the buckets of its running tests behind
        [AmazonS3Fact]
        public async Task GivenAS3AccountVerifyStaleTestBucketsAreDeleted()
        {
            using (var client = AmazonS3TestContext.CreateClient())
            {
                var errors = await TestBucketCleanup.DeleteStaleBucketsAsync(
                    client,
                    TestContext.Current.TestOutputHelper.WriteLine,
                    TestContext.Current.CancellationToken);

                errors.Should().BeEmpty();
            }
        }

        [AmazonS3Fact]
        public async Task GivenAFeedVerifyCleanupDeletesTheBucket()
        {
            await using (var testContext = new AmazonS3TestContext())
            {
                await testContext.InitAsync();

                var result = await InitCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.Logger,
                    token: TestContext.Current.CancellationToken);

                result.Should().BeTrue();

                await testContext.CleanupAsync();

                // A new file system, since HasBucket caches the result
                var fileSystem = await testContext.CreateFileSystemAsync();
                (await fileSystem.HasBucket(testContext.Logger, TestContext.Current.CancellationToken)).Should().BeFalse();

                // Deleting a bucket that doesn't exist succeeds, the same as for a test that fails before it creates its bucket
                await TestBucketCleanup.DeleteBucketAsync(testContext.Client, testContext.BucketName, TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public void GivenATestBucketVerifyItIsStaleAfterTheStaleAge()
        {
            var utcNow = DateTime.UtcNow;
            var bucketName = TestBucketCleanup.NewBucketName();
            var stale = utcNow - TestBucketCleanup.StaleAge - TimeSpan.FromMinutes(1);
            var inProgress = utcNow - TestBucketCleanup.StaleAge + TimeSpan.FromMinutes(1);

            TestBucketCleanup.IsStale(bucketName, stale, utcNow).Should().BeTrue();
            TestBucketCleanup.IsStale(bucketName, stale.ToLocalTime(), utcNow).Should().BeTrue();
            TestBucketCleanup.IsStale(bucketName, inProgress, utcNow).Should().BeFalse();
            TestBucketCleanup.IsStale(bucketName, inProgress.ToLocalTime(), utcNow).Should().BeFalse();
        }

        // RustFS lists a bucket that's being created with the Unix epoch as its creation date
        [Fact]
        public void GivenATestBucketWithAnUnknownCreationDateVerifyItIsNotStale()
        {
            var utcNow = DateTime.UtcNow;
            var bucketName = TestBucketCleanup.NewBucketName();

            TestBucketCleanup.IsStale(bucketName, creationDate: null, utcNow).Should().BeFalse();
            TestBucketCleanup.IsStale(bucketName, DateTime.UnixEpoch, utcNow).Should().BeFalse();
            TestBucketCleanup.IsStale(bucketName, DateTime.MinValue, utcNow).Should().BeFalse();
        }

        [Theory]
        [InlineData("sleet-test-feed")]
        [InlineData("sleet-test-0f8fad5b-d9cb-469f-a165-70867728950e-feed")]
        [InlineData("feed-sleet-test-0f8fad5b-d9cb-469f-a165-70867728950e")]
        [InlineData("feed")]
        public void GivenABucketThatIsNotATestBucketVerifyItIsNeverStale(string bucketName)
        {
            var utcNow = DateTime.UtcNow;

            TestBucketCleanup.IsStale(bucketName, utcNow.AddDays(-30), utcNow).Should().BeFalse();
        }
    }
}
