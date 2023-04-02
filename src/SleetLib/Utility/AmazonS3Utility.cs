using System;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

namespace Sleet
{
    public static class AmazonS3Utility
    {
        public static Uri GetBucketPath(string bucketName, string region)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals("us-east-1", region))
            {
                return new Uri($"https://s3.amazonaws.com/{bucketName}/");
            }

            return new Uri($"https://s3-{region}.amazonaws.com/{bucketName}/");
        }

        public static AWSCredentials LoadSsoCredentials()
        {
            var chain = new CredentialProfileStoreChain();
            if (!chain.TryGetAWSCredentials("my-sso-profile", out var credentials))
                throw new Exception("Failed to find the my-sso-profile profile");

            return credentials;
        }
    }
}
