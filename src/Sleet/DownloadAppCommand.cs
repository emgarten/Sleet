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
            var propertyOptions = cmd.Option(Constants.PropertyOption, Constants.PropertyDescription, CommandOptionType.MultipleValue);

            cmd.HelpOption(Constants.HelpOption);

            var outputPath = cmd.Option("-o|--output-path", "Output directory to store downloaded nupkgs.", CommandOptionType.SingleValue);
            var skipExisting = cmd.Option("--skip-existing", "Skip packages that already exist in the output folder.", CommandOptionType.NoValue);
            var noLock = cmd.Option("--no-lock", "Skip locking the feed and verifying the client version.", CommandOptionType.NoValue);
            var ignoreErrors = cmd.Option("--ignore-errors", "Ignore download errors.", CommandOptionType.NoValue);

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
                    var settings = LocalSettings.Load(optionConfigFile.Value(), SettingsUtility.GetPropertyMappings(propertyOptions.Values));
                    var fileSystem = Util.CreateFileSystemOrThrow(settings, sourceName.Value(), cache);

                    // Download packages
                    var success = await DownloadCommand.RunAsync(settings, fileSystem, outputPath.Value(), ignoreErrors.HasValue(), noLock.HasValue(), skipExisting.HasValue(), log);

                    return success ? 0 : 1;
                }
            });
        }
    }
}