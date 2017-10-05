using System.Collections.Generic;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Sleet
{
    internal static class RecreateAppCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("recreate", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Recreate a feed. This downloads all packages, deletes the feed, and then creates a new feed from the existing packages. This may be used to fix feed problems or to upgrade between Sleet versions.";

            var optionConfigFile = cmd.Option(Constants.ConfigOption, Constants.ConfigDesc,
                CommandOptionType.SingleValue);

            var sourceName = cmd.Option(Constants.SourceOption, Constants.SourceDesc,
                CommandOptionType.SingleValue);

            var verbose = cmd.Option(Constants.VerboseOption, Constants.VerboseDesc, CommandOptionType.NoValue);

            cmd.HelpOption(Constants.HelpOption);

            var nupkgPath = cmd.Option("--nupkg-path", "Optional temporary directory to store downloaded nupkgs in. This folder will be cleaned up if the command completes successfully. If the command fails these files will be left as a backup.", CommandOptionType.SingleValue);

            var force = cmd.Option("-f|--force", "Ignore errors when recreating the feed.", CommandOptionType.NoValue);

            var required = new List<CommandOption>();

            cmd.OnExecute(async () =>
            {
                // Validate parameters
                CmdUtils.VerifyRequiredOptions(required.ToArray());

                // Init logger
                Util.SetVerbosity(log, verbose.HasValue());

                // Create a temporary folder for caching files during the operation.
                using (var cache = new LocalCache())
                {
                    // Load settings and file system.
                    var settings = LocalSettings.Load(optionConfigFile.Value());
                    var fileSystem = Util.CreateFileSystemOrThrow(settings, sourceName.Value(), cache);

                    var tmpPath = nupkgPath.HasValue() ? nupkgPath.Value() : null;

                    // Run
                    var success = await RecreateCommand.RunAsync(settings, fileSystem, tmpPath, force.HasValue(), log);

                    return success ? 0 : 1;
                }
            });
        }
    }
}