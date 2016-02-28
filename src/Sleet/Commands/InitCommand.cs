using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Newtonsoft.Json.Linq;
using NuGet.Logging;

namespace Sleet
{
    internal static class InitCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("init", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Initialize a new sleet feed.";

            var optionConfigFile = cmd.Option("-c|--config", "sleet.json file to read sources and settings from.",
                CommandOptionType.SingleValue);

            var sourceConfigFile = cmd.Option("-s|--source", "Source from sleet.json.",
                CommandOptionType.SingleValue);

            cmd.HelpOption("-?|-h|--help");

            var required = new List<CommandOption>()
            {
                sourceConfigFile
            };

            cmd.OnExecute(() =>
            {
                cmd.ShowRootCommandFullNameAndVersion();

                // Validate parameters
                foreach (var requiredOption in required)
                {
                    if (!requiredOption.HasValue())
                    {
                        throw new ArgumentException($"Missing required parameter --{requiredOption.LongName}.");
                    }
                }

                return RunCore(null, null, log);
            });
        }

        public static async Task<int> RunCore(LocalSettings settings, ISleetFileSystem source, ILogger log)
        {
            var exitCode = 0;

            var now = DateTimeOffset.UtcNow.ToString("O");

            // Validate source

            // Check if already initialized

            // Create sleet.settings.json
            var sleetSettings = source.Get("sleet.settings.json");
            var sleetSettingsFile = await sleetSettings.GetLocal(log, CancellationToken.None);

            if (!sleetSettingsFile.Exists)
            {
                using (var writer = new StreamWriter(sleetSettingsFile.OpenWrite()))
                {
                    var json = new JObject();
                    json.Add("settings", new JArray());
                    json.Add("pinned", new JArray());
                    json.Add("created", new JValue(now));
                    json.Add("lastModified", new JValue(now));

                    writer.WriteLine(json.ToString());
                }
            }

            // Create index.json
            var index = source.Get("index.json");

            // Create empty files

            return exitCode;
        }
    }

    public static class InitCommandTestHook
    {
        public static Task<int> RunCore(LocalSettings settings, ISleetFileSystem source, ILogger log)
        {
            return InitCommand.RunCore(settings, source, log);
        }
    }
}
