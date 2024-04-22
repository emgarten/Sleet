using System.Collections.Generic;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Sleet
{
    internal static class DestroyAppCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("destroy", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Delete all files from a feed.";

            var optionConfigFile = cmd.Option(Constants.ConfigOption, Constants.ConfigDesc,
                CommandOptionType.SingleValue);

            var sourceName = cmd.Option(Constants.SourceOption, Constants.SourceDesc,
                CommandOptionType.SingleValue);

            var verbose = cmd.Option(Constants.VerboseOption, Constants.VerboseDesc, CommandOptionType.NoValue);
            var propertyOptions = cmd.Option(Constants.PropertyOption, Constants.PropertyDescription, CommandOptionType.MultipleValue);

            cmd.HelpOption(Constants.HelpOption);

            cmd.OnExecute(async () =>
            {
                // Init logger
                Util.SetVerbosity(log, verbose.HasValue());

                // Create a temporary folder for caching files during the operation.
                using (var cache = new LocalCache())
                {
                    // Load settings and file system.
                    var settings = LocalSettings.Load(optionConfigFile.Value(), SettingsUtility.GetPropertyMappings(propertyOptions.Values));
                    var fileSystem = await Util.CreateFileSystemOrThrow(settings, sourceName.Value(), cache, log);

                    // Delete files
                    var success = await DestroyCommand.RunAsync(settings, fileSystem, log);

                    return success ? 0 : 1;
                }
            });
        }
    }
}
