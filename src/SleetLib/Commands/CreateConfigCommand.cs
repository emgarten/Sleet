using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    public static class CreateConfigCommand
    {
        public static async Task<bool> RunAsync(FileSystemStorageType storageType, string? output, ILogger log)
        {
            return await RunAsync(storageType, output, provider: null, log);
        }

        public static async Task<bool> RunAsync(FileSystemStorageType storageType, string? output, string? provider, ILogger log)
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
                    {
                        var s3Provider = S3Provider.Get(provider);
                        storageTemplateJson = new JObject
                        {
                            { "name", s3Provider.TemplateFeedName },
                            { "type", "s3" }
                        };

                        if (!ReferenceEquals(s3Provider, S3Provider.Aws))
                        {
                            storageTemplateJson.Add("provider", s3Provider.Name);
                        }

                        storageTemplateJson.Add("bucketName", ReferenceEquals(s3Provider, S3Provider.Aws) ? "bucketname" : "myfeed");

                        foreach (var (key, value) in s3Provider.TemplateProperties)
                        {
                            storageTemplateJson.Add(key, value);
                        }

                        foreach (var note in s3Provider.SetupNotes)
                        {
                            log.Log(LogLevel.Minimal, note);
                        }
                    }
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
