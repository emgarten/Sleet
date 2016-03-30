using System;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Newtonsoft.Json.Linq;
using NuGet.Logging;

namespace Sleet
{
    internal static class CreateConfigCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("createconfig", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Create a new sleet.json config file.";

            var azure = cmd.Option("--azure", "Add a template entry for an azure storage feed.",
                CommandOptionType.NoValue);

            var folder = cmd.Option("--local", "Add a template entry for a local folder feed.",
                CommandOptionType.NoValue);

            var output = cmd.Option("--output", "Output path. If not specified the file will be created in the working directory.",
                CommandOptionType.SingleValue);

            cmd.HelpOption("-?|-h|--help");

            cmd.OnExecute(() =>
            {
                try
                {
                    cmd.ShowRootCommandFullNameAndVersion();

                    var outputPath = Directory.GetCurrentDirectory();

                    if (output.HasValue())
                    {
                        outputPath = output.Value();
                    }

                    outputPath = Path.GetFullPath(outputPath);

                    if (Directory.Exists(outputPath))
                    {
                        outputPath = Path.Combine(outputPath, "sleet.json");
                    }

                    if (File.Exists(outputPath))
                    {
                        log.LogError($"File already exists {outputPath}");
                        return 1;
                    }

                    if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
                    {
                        log.LogError($"Directory does not exist {Path.GetDirectoryName(outputPath)}");
                        return 1;
                    }

                    // Create the config template
                    var json = new JObject();

                    json.Add("username", "");
                    json.Add("useremail", "");

                    var sourcesArray = new JArray();
                    json.Add("sources", sourcesArray);

                    if (folder.HasValue())
                    {
                        var folderJson = new JObject();

                        folderJson.Add("name", "myLocalFeed");
                        folderJson.Add("type", "local");
                        folderJson.Add("path", Path.Combine(Directory.GetCurrentDirectory(), "myFeed"));

                        sourcesArray.Add(folderJson);
                    }

                    if (azure.HasValue())
                    {
                        var azureJson = new JObject();

                        azureJson.Add("name", "myAzureFeed");
                        azureJson.Add("type", "azure");
                        azureJson.Add("path", "https://yourStorageAccount.blob.core.windows.net/myFeed/");
                        azureJson.Add("container", "myFeed");
                        azureJson.Add("connectionString", "DefaultEndpointsProtocol=https;AccountName=;AccountKey=;BlobEndpoint=");

                        sourcesArray.Add(azureJson);
                    }

                    JsonUtility.SaveJson(new FileInfo(outputPath), json);

                    log.LogMinimal($"Writing config template to {outputPath}");

                    log.LogMinimal("Modify this template by changing the name and path for your own feed.");

                    return 0;
                }
                catch (Exception ex)
                {
                    log.LogError(ex.Message);
                    log.LogDebug(ex.ToString());
                }

                return 1;
            });
        }
    }
}
