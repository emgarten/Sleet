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
                        { "path", Path.Combine(Directory.GetCurrentDirectory(), "myFeed") }
                    };
                    break;
                case FileSystemStorageType.Azure:
                    storageTemplateJson = new JObject
                    {
                        { "name", "myAzureFeed" },
                        { "type", "azure" },
                        { "path", "https://yourStorageAccount.blob.core.windows.net/myFeed/" },
                        { "container", "myFeed" },
                        { "connectionString", AzureFileSystem.AzureEmptyConnectionString }
                    };
                    break;
                case FileSystemStorageType.AmazonS3:
                    storageTemplateJson = new JObject
                    {
                        { "name", "myAmazonS3Feed" },
                        { "type", "s3" },
                        { "path", "https://s3.amazonaws.com/bucketname/" },
                        { "bucketName", "bucketname" },
                        { "region", "us-east-1" },
                        { "accessKeyId", "" },
                        { "secretAccessKey", "" }
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