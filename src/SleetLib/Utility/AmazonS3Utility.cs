using System;
using System.Collections.Generic;
using System.Text;

namespace Sleet
{
    public static class AmazonS3Utility
    {
        public static Uri GetBucketPath(string bucketName, string region)
        {
            return new Uri($"https://{bucketName}.s3.{region}.amazonaws.com/");
        }
    }
}
