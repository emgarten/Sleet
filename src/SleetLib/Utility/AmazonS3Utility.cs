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
    }
}
