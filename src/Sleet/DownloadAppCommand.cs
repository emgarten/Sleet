using System.Collections.Generic;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Sleet
{
    internal static class DownloadAppCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("download", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Download packages from a feed to a local folder.";

            var optionConfigFile = cmd.Option(Constants.ConfigOption, Constants.ConfigDesc,
                CommandOptionType.SingleValue);

            var sourceName = cmd.Option(Constants.SourceOption, Constants.SourceDesc,
                CommandOptionType.SingleValue);

            var verbose = cmd.Option(Constants.VerboseOption, Constants.VerboseDesc, CommandOptionType.NoValue);

            cmd.HelpOption(Constants.HelpOption);

            var outputPath = cmd.Option("-o|--output-path", "Output directory to store downloaded nupkgs.", CommandOptionType.SingleValue);

            var required = new List<CommandOption>()
            {
                outputPath
            };

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

                    // Download packages
                    var success = await DownloadCommand.RunAsync(settings, fileSystem, outputPath.Value(), ignoreErrors: false, log: log);

                    return success ? 0 : 1;
                }
            });
        }
    }
}