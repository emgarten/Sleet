using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Sleet
{
    internal static class PushCommand
    {
        public static void Register(CommandLineApplication cmdApp)
        {
            cmdApp.Command("push", Run, throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd)
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

                // Validate package

                // Validate source

                // Check if already initialized

                // Get sleet.settings.json

                // Prune

                // Add to catalog

                // Registration

                // Flat container

                // Search

                // Save all files

                return 0;
            });
        }
    }
}
