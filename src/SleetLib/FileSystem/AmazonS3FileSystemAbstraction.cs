#if !SLEETLEGACY
using System;
using System.Collections.Generic;
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
        private const int MaximumNumberOfObjectsToFetch = 100;

        public static Task CreateFileAsync(
            IAmazonS3 client,
            string bucketName,
            string key,
            string contentBody,
            ServerSideEncryptionMethod serverSideEncryptionMethod,
            CancellationToken token)
        {
            var putObjectRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                ContentBody = contentBody,
                ServerSideEncryptionMethod = serverSideEncryptionMethod
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
            using (var response = await client
                .GetObjectAsync(bucketName, key, token)
                .ConfigureAwait(false))
            using (var responseStream = response.ResponseStream)
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
            var listObjectsResponse = await client
                .ListObjectsV2Async(listObjectsRequest, token)
                .ConfigureAwait(false);

            return listObjectsResponse.S3Objects
                .Any(x => x.Key.Equals(key, StringComparison.Ordinal));
        }

        public static async Task<List<S3Object>> GetFilesAsync(
            IAmazonS3 client,
            string bucketName,
            CancellationToken token)
        {
            List<S3Object> s3Objects = null;
            var listObjectsRequest = new ListObjectsV2Request
            {
                BucketName = bucketName,
                MaxKeys = MaximumNumberOfObjectsToFetch,
            };

            ListObjectsV2Response listObjectsResponse;
            do
            {
                listObjectsResponse = await client.ListObjectsV2Async(listObjectsRequest, token).ConfigureAwait(false);
                listObjectsRequest.ContinuationToken = listObjectsResponse.NextContinuationToken;

                if (s3Objects == null)
                    s3Objects = listObjectsResponse.S3Objects;
                else
                    s3Objects.AddRange(listObjectsResponse.S3Objects);
            } while (listObjectsResponse.IsTruncated);

            return s3Objects;
        }

        public static Task RemoveFileAsync(IAmazonS3 client, string bucketName, string key, CancellationToken token)
        {
            return client.DeleteObjectAsync(bucketName, key, token);
        }

        public static Task RemoveMultipleFilesAsync(
            IAmazonS3 client,
            string bucketName,
            IEnumerable<KeyVersion> objects,
            CancellationToken token)
        {
            var request = new DeleteObjectsRequest
            {
                BucketName = bucketName,
                Objects = objects.ToList(),
            };

            return request.Objects.Count == 0
                ? TaskUtils.CompletedTask
                : client.DeleteObjectsAsync(request, token);
        }

        public static async Task UploadFileAsync(
            IAmazonS3 client,
            string bucketName,
            string key,
            string contentType,
            string contentEncoding,
            Stream reader,
            ServerSideEncryptionMethod serverSideEncryptionMethod,
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
                Headers = { CacheControl = "no-store" },
                ServerSideEncryptionMethod = serverSideEncryptionMethod
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
#endif