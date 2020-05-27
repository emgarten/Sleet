using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    public static class CreateConfigCommand
    {
        public static async Task<bool> RunAsync(FileSystemStorageType storageType, string output, ILogger log)
        {
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

            if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
            {
                log.LogError($"Directory does not exist {Path.GetDirectoryName(outputPath)}");
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

            JObject storageTemplateJson = null;
            switch (storageType)
            {
                case FileSystemStorageType.Local:
                    storageTemplateJson = new JObject
                    {
                        { "name", "myLocalFeed" },
                        { "type", "local" },
                        { "path", Path.Combine(Directory.GetCurrentDirectory(), "myFeed") },
                        { "baseURI", "https://example.com/feed/" }
                    };
                    break;
                case FileSystemStorageType.Azure:
                    storageTemplateJson = new JObject
                    {
                        { "name", "myAzureFeed" },
                        { "type", "azure" },
                        { "container", "myFeed" },
                        { "connectionString", AzureFileSystem.AzureEmptyConnectionString }
                    };
                    break;
                case FileSystemStorageType.S3:
                    storageTemplateJson = new JObject
                    {
                        { "name", "myAmazonS3Feed" },
                        { "type", "s3" },
                        { "bucketName", "bucketname" },
                        { "region", "us-east-1" },
                        { "profileName", "credentialsFileProfileName" }
                    };
                    log.Log(LogLevel.Minimal, "AWS credentials can be specified directly in sleet.json using accessKeyId and secretAccessKey instead of profileName. By default sleet.json is set to use a credentials file profile. To configure keys see: https://docs.aws.amazon.com/sdk-for-net/v2/developer-guide/net-dg-config-creds.html#creds-file");
                    break;
                case FileSystemStorageType.MinioS3:
                    storageTemplateJson = new JObject
                    {
                        { "name", "myMinioS3Feed" },
                        { "type", "minio" },
                        { "compress", "false" },
                        { "bucketName", "bucketname" },
                        { "region", "us-east-1" },
                        { "serviceURL", "http://localhost:9000" },
                        { "accessKeyId", "MINIO_ACCESS_KEY" },
                        { "secretAccessKey", "MINIO_SECRET_ACCESS_KEY" }
                    };
                    log.Log(LogLevel.Minimal, "MinIO Server credentials can be specified in AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY environment variables instead of using accessKeyId and secretAccessKey in sleet.json. By default, sleet.json includes accessKeyId and secretAccessKey. CAUTION: compress MUST equal 'false'.");
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
    }
}
