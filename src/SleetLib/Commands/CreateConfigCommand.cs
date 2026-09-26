using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    public static class CreateConfigCommand
    {
        public static Task<bool> RunAsync(FileSystemStorageType storageType, string? output, ILogger log)
        {
            return RunAsync(storageType, output, provider: null, log);
        }

        /// <summary>
        /// Create a sleet.json template.
        /// </summary>
        /// <param name="provider">S3 compatible service for S3 feeds: aws, r2, or minio. Null uses aws.</param>
        public static async Task<bool> RunAsync(FileSystemStorageType storageType, string? output, string? provider, ILogger log)
        {
            var s3Provider = S3Provider.Get(provider);
            var outputPath = Directory.GetCurrentDirectory();

            if (!string.IsNullOrEmpty(output))
            {
                outputPath = output;
            }

            outputPath = Path.GetFullPath(outputPath);

            if (Directory.Exists(outputPath))
            {
                outputPath = Path.Combine(outputPath, "sleet.json");
            }

            if (File.Exists(outputPath))
            {
                log.LogError($"File already exists {outputPath}");
                return false;
            }

            var outputDir = Path.GetDirectoryName(outputPath);
            if (outputDir is null || !Directory.Exists(outputDir))
            {
                log.LogError($"Directory does not exist {outputDir}");
                return false;
            }

            // Create the config template
            var json = new JObject
            {
                { "username", "" },
                { "useremail", "" }
            };

            var sourcesArray = new JArray();
            json.Add("sources", sourcesArray);

            JObject? storageTemplateJson = null;
            switch (storageType)
            {
                case FileSystemStorageType.Local:
                    storageTemplateJson = new JObject
                    {
                        { "name", "myLocalFeed" },
                        { "type", "local" },
                        { "path", Path.Combine(Directory.GetCurrentDirectory(), "myfeed") },
                        { "baseURI", "https://example.com/feed/" }
                    };
                    break;
                case FileSystemStorageType.Azure:
                    storageTemplateJson = new JObject
                    {
                        { "name", "myAzureFeed" },
                        { "type", "azure" },
                        { "container", "myfeed" },
                        { "connectionString", AzureFileSystem.AzureEmptyConnectionString }
                    };
                    break;
                case FileSystemStorageType.S3:
                    storageTemplateJson = GetS3Template(s3Provider, log);
                    break;
                case FileSystemStorageType.Unspecified:
                    storageTemplateJson = new JObject
                    {
                        { "name", "myFeed" },
                        { "type", "" }
                    };
                    break;
            }

            if (storageTemplateJson != null)
                sourcesArray.Add(storageTemplateJson);

            await log.LogAsync(LogLevel.Minimal, $"Writing config template to {outputPath}");
            File.WriteAllText(outputPath, json.ToString());

            await log.LogAsync(LogLevel.Minimal, "Modify this template by changing the name and path for your own feed.");

            return true;
        }

        private static JObject GetS3Template(S3Provider provider, ILogger log)
        {
            if (provider == S3Provider.CloudflareR2)
            {
                log.Log(LogLevel.Minimal, "Set serviceURL to the S3 API url of your Cloudflare account, baseURI to the public url of the bucket, and accessKeyId and secretAccessKey to an R2 API token. See: https://github.com/emgarten/Sleet/blob/main/doc/feed-type-cloudflare.md");

                return new JObject
                {
                    { "name", "myR2Feed" },
                    { "type", "s3" },
                    { "provider", provider.Name },
                    { "bucketName", "bucketname" },
                    { "serviceURL", "https://ACCOUNT_ID.r2.cloudflarestorage.com" },
                    { "baseURI", "https://nuget.example.com/" },
                    { "accessKeyId", "" },
                    { "secretAccessKey", "" }
                };
            }

            if (provider == S3Provider.Minio)
            {
                log.Log(LogLevel.Minimal, "Set serviceURL to the url of your MinIO server, and accessKeyId and secretAccessKey to a MinIO access key.");

                return new JObject
                {
                    { "name", "myMinioFeed" },
                    { "type", "s3" },
                    { "provider", provider.Name },
                    { "bucketName", "bucketname" },
                    { "serviceURL", "http://localhost:9000" },
                    { "accessKeyId", "" },
                    { "secretAccessKey", "" }
                };
            }

            log.Log(LogLevel.Minimal, "AWS credentials can be specified directly in sleet.json using accessKeyId and secretAccessKey instead of profileName. By default sleet.json is set to use a credentials file profile. To configure keys see: https://docs.aws.amazon.com/sdk-for-net/v2/developer-guide/net-dg-config-creds.html#creds-file");

            return new JObject
            {
                { "name", "myAmazonS3Feed" },
                { "type", "s3" },
                { "bucketName", "bucketname" },
                { "region", "us-east-1" },
                { "profileName", "credentialsFileProfileName" }
            };
        }
    }
}
