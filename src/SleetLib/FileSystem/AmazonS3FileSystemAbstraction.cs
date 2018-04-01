using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace Sleet
{
    public static class AmazonS3FileSystemAbstraction
    {
        public const int DefaultCopyBufferSize = 81920;

        public static Task CreateFileAsync(
            IAmazonS3 client,
            string bucketName,
            string key,
            string contentBody,
            CancellationToken token)
        {
            var putObjectRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                ContentBody = contentBody,
            };

            return client.PutObjectAsync(putObjectRequest, token);
        }

        public static async Task<string> DownloadFileAsync(
            IAmazonS3 client,
            string bucketName,
            string key,
            Stream writer,
            CancellationToken token)
        {
            using (GetObjectResponse response = await client
                .GetObjectAsync(bucketName, key, token)
                .ConfigureAwait(false))
            using (Stream responseStream = response.ResponseStream)
            {
                await responseStream.CopyToAsync(writer, DefaultCopyBufferSize, token).ConfigureAwait(false);
                return response.Headers.ContentEncoding;
            }
        }

        public static async Task<bool> FileExistsAsync(
            IAmazonS3 client,
            string bucketName,
            string key,
            CancellationToken token)
        {
            var listObjectsRequest = new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = key,
            };
            ListObjectsV2Response listObjectsResponse = await client
                .ListObjectsV2Async(listObjectsRequest, token)
                .ConfigureAwait(false);

            return listObjectsResponse.S3Objects
                .Any(x => x.Key.Equals(key, StringComparison.Ordinal));
        }

        public static Task RemoveFileAsync(IAmazonS3 client, string bucketName, string key, CancellationToken token)
        {
            return client.DeleteObjectAsync(bucketName, key, token);
        }

        public static async Task UploadFileAsync(
            IAmazonS3 client,
            string bucketName,
            string key,
            string contentType,
            string contentEncoding,
            Stream reader,
            CancellationToken token)
        {
            var transferUtility = new TransferUtility(client);
            var request = new TransferUtilityUploadRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = reader,
                AutoCloseStream = false,
                AutoResetStreamPosition = false,
                Headers = { CacheControl = "no-store" }
            };

            if (contentType != null)
            {
                request.ContentType = contentType;
                request.Headers.ContentType = contentType;
            }

            if (contentEncoding != null)
                request.Headers.ContentEncoding = contentEncoding;

            using (transferUtility)
                await transferUtility.UploadAsync(request, token).ConfigureAwait(false);
        }
    }
}