using Amazon.S3;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// Grants public read access to a newly created bucket. S3 compatible services implement
    /// different subsets of the S3 API, each service uses the strategy it supports.
    /// </summary>
    public interface IS3PublicAccessStrategy
    {
        /// <summary>
        /// Configure public read access on a bucket that was just created.
        /// </summary>
        Task ConfigureBucketAsync(IAmazonS3 client, string bucketName, S3CannedACL? acl, ILogger log, CancellationToken token);
    }
}
