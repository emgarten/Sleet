using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;

namespace Sleet.AmazonS3.Tests
{
    /// <summary>
    /// Deletes the test buckets. A run that's stopped before it finishes, such as a cancelled CI run, can't delete the
    /// buckets of the tests that were running, so later runs delete them once they're stale.
    /// </summary>
    public static class TestBucketCleanup
    {
        public const string NamePrefix = "sleet-test-";

        /// <summary>
        /// A test bucket that's older than this was left behind. It's longer than the CI job timeout, so the runs that are
        /// still in progress keep their buckets.
        /// </summary>
        public static readonly TimeSpan StaleAge = TimeSpan.FromHours(2);

        // The most keys that a DeleteObjects request can delete
        private const int MaxKeys = 1000;

        private const int MaxAttempts = 4;

        public static string NewBucketName()
        {
            return NamePrefix + Guid.NewGuid().ToString("D");
        }

        /// <summary>
        /// True if the bucket has a name from NewBucketName and it's older than StaleAge.
        /// </summary>
        public static bool IsStale(string bucketName, DateTime? creationDate, DateTime utcNow)
        {
            var isTestBucket = bucketName?.StartsWith(NamePrefix, StringComparison.Ordinal) == true
                && Guid.TryParseExact(bucketName.AsSpan(NamePrefix.Length), "D", out _);

            // The creation date isn't known if it's missing or at the Unix epoch. RustFS lists a bucket that's being
            // created with the Unix epoch, which would make the buckets of the tests that are running look stale.
            var created = creationDate?.ToUniversalTime();

            return isTestBucket && created > DateTime.UnixEpoch && utcNow - created > StaleAge;
        }

        /// <summary>
        /// Delete the stale test buckets of the account. Returns an error for each bucket that couldn't be deleted.
        /// </summary>
        public static async Task<List<string>> DeleteStaleBucketsAsync(IAmazonS3 client, Action<string> log, CancellationToken token)
        {
            var utcNow = DateTime.UtcNow;
            var staleBuckets = (await ListBucketsAsync(client, token))
                .Where(x => IsStale(x.BucketName, x.CreationDate, utcNow))
                .ToList();

            log($"Found {staleBuckets.Count} test buckets that are more than {StaleAge.TotalHours} hours old.");

            var errors = new ConcurrentQueue<string>();
            var options = new ParallelOptions() { MaxDegreeOfParallelism = 8, CancellationToken = token };

            await Parallel.ForEachAsync(staleBuckets, options, async (bucket, cancellationToken) =>
            {
                try
                {
                    await DeleteBucketAsync(client, bucket.BucketName, cancellationToken);
                    log($"Deleted {bucket.BucketName}, created {bucket.CreationDate:u}");
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    errors.Enqueue($"{bucket.BucketName}: {ex.Message}");
                }
            });

            return errors.ToList();
        }

        /// <summary>
        /// Delete the bucket and its objects. Succeeds if the bucket doesn't exist.
        /// </summary>
        public static async Task DeleteBucketAsync(IAmazonS3 client, string bucketName, CancellationToken token)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await DeleteObjectsAsync(client, bucketName, token);
                    await client.DeleteBucketAsync(bucketName, token);
                    return;
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound && attempt > 1)
                {
                    // The test didn't create the bucket, or it's already deleted
                    return;
                }
                catch (Exception) when (attempt < MaxAttempts && !token.IsCancellationRequested)
                {
                    // Retry the conflicts, such as a bucket that was just created or changed, or an object that was
                    // written after the list. The first not found is also retried, since Amazon S3 can briefly report a
                    // new bucket as not found. The SDK already retries the transient errors of each request.
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), token);
                }
            }
        }

        private static async Task<List<S3Bucket>> ListBucketsAsync(IAmazonS3 client, CancellationToken token)
        {
            var buckets = new List<S3Bucket>();
            var request = new ListBucketsRequest();

            do
            {
                var response = await client.ListBucketsAsync(request, token);
                buckets.AddRange(response.Buckets ?? []);
                request.ContinuationToken = response.ContinuationToken;
            }
            while (!string.IsNullOrEmpty(request.ContinuationToken));

            return buckets;
        }

        private static async Task DeleteObjectsAsync(IAmazonS3 client, string bucketName, CancellationToken token)
        {
            var request = new ListObjectsV2Request() { BucketName = bucketName, MaxKeys = MaxKeys };
            ListObjectsV2Response response;

            do
            {
                response = await client.ListObjectsV2Async(request, token);
                var objects = response.S3Objects?.Select(x => new KeyVersion() { Key = x.Key }).ToList() ?? [];

                if (objects.Count > 0)
                {
                    await client.DeleteObjectsAsync(new DeleteObjectsRequest() { BucketName = bucketName, Objects = objects, Quiet = true }, token);
                }

                request.ContinuationToken = response.NextContinuationToken;
            }
            while (response.IsTruncated == true);
        }
    }
}
