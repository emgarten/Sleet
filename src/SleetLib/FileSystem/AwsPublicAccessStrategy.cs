using Amazon.S3;
using Amazon.S3.Model;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// Grants public read access the way Amazon S3 requires. Public access blocks and object
    /// ownership must be set before a public bucket policy will be accepted.
    /// </summary>
    public class AwsPublicAccessStrategy : BucketPolicyPublicAccessStrategy
    {
        public AwsPublicAccessStrategy(S3Provider provider)
            : base(provider)
        {
        }

        public override async Task ConfigureBucketAsync(IAmazonS3 client, string bucketName, S3CannedACL? acl, ILogger log, CancellationToken token)
        {
            // As of 2023-04 additional settings are needed to allow a public policy
            // https://stackoverflow.com/questions/39085360/why-is-uploading-a-file-to-s3-via-the-c-sharp-aws-sdk-giving-a-permission-denied

            // Account wide settings can also block public access, users must update their accounts manually for this
            // https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/aws-properties-s3-bucket-publicaccessblockconfiguration.html

            // Remove all public access blocks for this bucket
            await SetPublicAccessBlocksAsync(client, bucketName, log, token);

            // Set ownership preference to ensure we can set a public policy
            await SetOwnershipAsync(client, bucketName, log, token);

            // Set the public policy to public read-only, and the acl if one was given.
            await base.ConfigureBucketAsync(client, bucketName, acl, log, token);
        }

        /// <summary>
        /// Remove public access blocks to allow public policies. Amazon specific, services that
        /// do not implement it can still accept the bucket policy.
        /// </summary>
        private Task<bool> SetPublicAccessBlocksAsync(IAmazonS3 client, string bucketName, ILogger log, CancellationToken token)
        {
            return S3ErrorUtility.TryApplyAsync((_, t) =>
            {
                var blockReq = new PutPublicAccessBlockRequest()
                {
                    BucketName = bucketName,
                    PublicAccessBlockConfiguration = new PublicAccessBlockConfiguration()
                    {
                        IgnorePublicAcls = false,
                        RestrictPublicBuckets = false,
                        BlockPublicAcls = false,
                        BlockPublicPolicy = false
                    }
                };

                return client.PutPublicAccessBlockAsync(blockReq, t);
            }, "public access blocks", Provider, log, token);
        }

        /// <summary>
        /// Set ObjectOwnership to BucketOwnerPreferred to allow for setting the public access policy.
        /// </summary>
        private Task<bool> SetOwnershipAsync(IAmazonS3 client, string bucketName, ILogger log, CancellationToken token)
        {
            return S3ErrorUtility.TryApplyAsync((_, t) =>
            {
                var ownerReq = new PutBucketOwnershipControlsRequest()
                {
                    BucketName = bucketName,
                    OwnershipControls = new OwnershipControls()
                    {
                        Rules = new List<OwnershipControlsRule>
                        {
                            new() {
                                ObjectOwnership = ObjectOwnership.BucketOwnerPreferred
                            }
                        }
                    }
                };

                return client.PutBucketOwnershipControlsAsync(ownerReq, t);
            }, "object ownership controls", Provider, log, token);
        }
    }
}
