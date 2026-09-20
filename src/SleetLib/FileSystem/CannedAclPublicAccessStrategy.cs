using Amazon.S3;
using Amazon.S3.Model;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// Grants public read access with a canned acl. Used by services that implement PutBucketAcl
    /// but not bucket policies.
    /// </summary>
    public class CannedAclPublicAccessStrategy : IS3PublicAccessStrategy
    {
        private readonly S3Provider _provider;

        public CannedAclPublicAccessStrategy(S3Provider provider)
        {
            _provider = provider;
        }

        public async Task ConfigureBucketAsync(IAmazonS3 client, string bucketName, S3CannedACL? acl, ILogger log, CancellationToken token)
        {
            // Fall back to public read, the feed is not usable by NuGet clients without it.
            var resolvedAcl = acl ?? S3CannedACL.PublicRead;

            // This is what makes the feed readable, a service that cannot apply it fails rather
            // than leaving the bucket private.
            await S3ErrorUtility.RetryAsync((_, t) =>
            {
                var bucketAclReq = new PutBucketAclRequest()
                {
                    BucketName = bucketName,
                    ACL = resolvedAcl
                };

                return client.PutBucketAclAsync(bucketAclReq, t);
            }, _provider, log, token);

            log.LogInformation($"Set acl '{resolvedAcl.Value}' for public read access on bucket: {bucketName}");
        }
    }
}
