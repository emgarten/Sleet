using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// Grants public read access with a bucket policy. Used by services that implement
    /// PutBucketPolicy but not the public access block or object ownership APIs.
    /// </summary>
    public class BucketPolicyPublicAccessStrategy : IS3PublicAccessStrategy
    {
        protected S3Provider Provider { get; }

        public BucketPolicyPublicAccessStrategy(S3Provider provider)
        {
            Provider = provider;
        }

        public virtual async Task ConfigureBucketAsync(IAmazonS3 client, string bucketName, S3CannedACL? acl, ILogger log, CancellationToken token)
        {
            await SetBucketPolicyAsync(client, bucketName, log, token);

            if (acl != null)
            {
                await SetBucketAclAsync(client, bucketName, acl, log, token);
            }

            log.LogInformation($"Added policy for public read access to bucket: {bucketName}");
        }

        /// <summary>
        /// Set the bucket policy to public read-only. This is what makes the feed readable, a
        /// service that cannot apply it fails rather than leaving the bucket private.
        /// </summary>
        protected Task SetBucketPolicyAsync(IAmazonS3 client, string bucketName, ILogger log, CancellationToken token)
        {
            return S3ErrorUtility.RetryAsync((_, t) =>
            {
                var policyRequest = new PutBucketPolicyRequest()
                {
                    BucketName = bucketName,
                    Policy = GetReadOnlyPolicy(bucketName).ToString(),
                };

                return client.PutBucketPolicyAsync(policyRequest, t);
            }, Provider, log, token);
        }

        /// <summary>
        /// Set the default acl of the bucket. Explicitly requested by the user, so a failure is
        /// reported rather than skipped.
        /// </summary>
        protected Task SetBucketAclAsync(IAmazonS3 client, string bucketName, S3CannedACL acl, ILogger log, CancellationToken token)
        {
            return S3ErrorUtility.RetryAsync((_, t) =>
            {
                var bucketAclReq = new PutBucketAclRequest()
                {
                    BucketName = bucketName,
                    ACL = acl
                };

                return client.PutBucketAclAsync(bucketAclReq, t);
            }, Provider, log, token);
        }

        /// <summary>
        /// Read-only bucket policy document. Services using a policy schema that is not
        /// compatible with the AWS schema can override this.
        /// </summary>
        protected virtual JObject GetReadOnlyPolicy(string bucketName)
        {
            // Grant read-only access based on https://aws.amazon.com/premiumsupport/knowledge-center/read-access-objects-s3-bucket/
            return new JObject(
                new JProperty("Version", "2012-10-17"),
                new JProperty("Statement",
                    new JArray(
                        new JObject(
                            new JProperty("Sid", "AllowPublicRead"),
                            new JProperty("Effect", "Allow"),
                            new JProperty("Principal", "*"),
                            new JProperty("Action", new JArray("s3:GetObject")),
                            new JProperty("Resource", new JArray($"arn:aws:s3:::{bucketName}/*"))
                        ))));
        }
    }
}
