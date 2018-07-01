using System.Collections.Generic;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Sleet
{
    internal static class DeleteAppCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("delete", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Delete a package or packages from a feed.";

            var optionConfigFile = cmd.Option(Constants.ConfigOption, Constants.ConfigDesc,
                CommandOptionType.SingleValue);

            var sourceName = cmd.Option(Constants.SourceOption, Constants.SourceDesc,
                CommandOptionType.SingleValue);

            var packageId = cmd.Option("-i|--id", "Package id.",
                CommandOptionType.SingleValue);

            var version = cmd.Option("-v|--version", "Package version. If this is not specified all versions will be deleted.",
                CommandOptionType.SingleValue);

            var reason = cmd.Option("-r|--reason", "Reason for deleting the package.", CommandOptionType.SingleValue);

            var force = cmd.Option("-f|--force", "Ignore missing packages.", CommandOptionType.NoValue);

            var verbose = cmd.Option(Constants.VerboseOption, Constants.VerboseDesc, CommandOptionType.NoValue);
            var propertyOptions = cmd.Option(Constants.PropertyOption, Constants.PropertyDescription, CommandOptionType.MultipleValue);

            cmd.HelpOption(Constants.HelpOption);

            var required = new List<CommandOption>()
            {
                packageId
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

                    var success = await DeleteCommand.RunAsync(settings, fileSystem, packageId.Value(), version.Value(), reason.Value(), force.HasValue(), log);

                    return success ? 0 : 1;
                }
            });
        }
    }
}