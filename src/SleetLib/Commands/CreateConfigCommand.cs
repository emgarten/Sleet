using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    public static class CreateConfigCommand
    {
        public static async Task<bool> RunAsync(bool isAzure, bool isLocal, string output, ILogger log)
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

            if (isLocal)
            {
                var folderJson = new JObject
                {
                    { "name", "myLocalFeed" },
                    { "type", "local" },
                    { "path", Path.Combine(Directory.GetCurrentDirectory(), "myFeed") }
                };
                sourcesArray.Add(folderJson);
            }

            if (isAzure)
            {
                var azureJson = new JObject
                {
                    { "name", "myAzureFeed" },
                    { "type", "azure" },
                    { "path", "https://yourStorageAccount.blob.core.windows.net/myFeed/" },
                    { "container", "myFeed" },
                    { "connectionString", "DefaultEndpointsProtocol=https;AccountName=;AccountKey=;BlobEndpoint=" }
                };
                sourcesArray.Add(azureJson);
            }

            await JsonUtility.SaveJsonAsync(new FileInfo(outputPath), json);

            log.LogMinimal($"Writing config template to {outputPath}");

            log.LogMinimal("Modify this template by changing the name and path for your own feed.");

            return true;
        }
    }
}