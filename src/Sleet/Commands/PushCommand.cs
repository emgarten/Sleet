using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Logging;

namespace Sleet
{
    internal static class PushCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("push", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Push a package to a feed.";

            var optionConfigFile = cmd.Option("-c|--config", "sleet.json file to read sources and settings from.",
                CommandOptionType.SingleValue);

            var sourceName = cmd.Option("-s|--source", "Source name from sleet.json.",
                            CommandOptionType.SingleValue);

            var argRoot = cmd.Argument(
                "[root]",
                "Paths to individual packages or directories containing packages.",
                multipleValues: true);

            cmd.HelpOption("-?|-h|--help");

            var required = new List<CommandOption>()
            {
                sourceName
            };

            cmd.OnExecute(async () =>
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

                var settings = LocalSettings.Load(optionConfigFile.Value());

                using (var cache = new LocalCache())
                {
                    var fileSystem = FileSystemFactory.CreateFileSystem(settings, cache, sourceName.Value());

                    if (fileSystem == null)
                    {
                        throw new InvalidOperationException("Unable to find source. Verify that the --source parameter is correct and that sleet.json contains the named source.");
                    }

                    return await RunCore(settings, fileSystem, log);
                }
            });
        }

        public static async Task<int> RunCore(LocalSettings settings, ISleetFileSystem source, ILogger log)
        {
            var exitCode = 0;

            var token = CancellationToken.None;
            var now = DateTimeOffset.UtcNow;

            // Validate package

            // Validate source
            await UpgradeUtility.UpgradeIfNeeded(source, log, token);

            // Check if already initialized

            // Get sleet.settings.json

            // Prune

            // Add to catalog

            // Registration

            // Flat container

            // Search

            // Save all files

            // Save all
            await source.Commit(log, token);

            return exitCode;
        }
    }
}
