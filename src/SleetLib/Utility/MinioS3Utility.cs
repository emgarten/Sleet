using System;

namespace Sleet
{
    public static class MinioS3Utility
    {
        public static Uri GetBucketPath(string bucketName, string serviceURL)
        {
            return new Uri($"{serviceURL}/{bucketName}/");
        }
    }
}
