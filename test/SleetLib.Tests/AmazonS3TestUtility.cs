using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Moq;

namespace SleetLib.Tests
{
    internal static class AmazonS3TestUtility
    {
        internal const string BucketName = "test-bucket";

        internal static Mock<IAmazonS3> CreateClient()
        {
            var client = new Mock<IAmazonS3>(MockBehavior.Loose);
            client.SetupGet(c => c.Config).Returns(new AmazonS3Config());
            return client;
        }

        internal static byte[] ReadAll(Stream stream)
        {
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        internal static string ReadUtf8(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        internal static string ReadGzipUtf8(byte[] bytes)
        {
            using (var memoryStream = new MemoryStream(bytes))
            using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzipStream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        internal static byte[] GzipUtf8(string text)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, leaveOpen: true))
                using (var writer = new StreamWriter(gzipStream, Encoding.UTF8))
                {
                    writer.Write(text);
                }

                return memoryStream.ToArray();
            }
        }

        internal static Uri RootUri()
        {
            return new Uri("https://test-bucket.s3.amazonaws.com/");
        }

        internal static AmazonS3Exception S3Exception(System.Net.HttpStatusCode statusCode, string message = "failed")
        {
            return new AmazonS3Exception(message) { StatusCode = statusCode };
        }
    }
}
