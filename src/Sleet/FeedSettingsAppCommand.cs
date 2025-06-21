using System.Collections.Generic;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Sleet
{
    internal static class FeedSettingsAppCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("feed-settings", cmd => Run(cmd, log));
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Read or modify feed settings stored in sleet.settings.json for the feed.";

            var optionConfigFile = cmd.Option(Constants.ConfigOption, Constants.ConfigDesc,
                CommandOptionType.SingleValue);

            var sourceName = cmd.Option(Constants.SourceOption, Constants.SourceDesc,
                CommandOptionType.SingleValue);

            var verbose = cmd.Option(Constants.VerboseOption, Constants.VerboseDesc, CommandOptionType.NoValue);

            cmd.HelpOption(Constants.HelpOption);

            var unsetAll = cmd.Option("--unset-all", "Clear all feed settings.", CommandOptionType.NoValue);
            var unset = cmd.Option("--unset", "Remove a feed setting. May be specified multiple times.", CommandOptionType.MultipleValue);
            var setSetting = cmd.Option("--set", "Add a feed setting. Value must be in the form {key}:{value}  May be specified multiple times.", CommandOptionType.MultipleValue);
            var getSetting = cmd.Option("--get", "Display a feed setting. May be specified multiple times.", CommandOptionType.MultipleValue);
            var getAll = cmd.Option("--get-all", "Diplay all feed settings.", CommandOptionType.NoValue);
            var propertyOptions = cmd.Option(Constants.PropertyOption, Constants.PropertyDescription, CommandOptionType.MultipleValue);

            var required = new List<CommandOption>();

            cmd.OnExecuteAsync(async _ =>
            {
                // Validate parameters
                CmdUtils.VerifyRequiredOptions(required.ToArray());

                // Init logger
                Util.SetVerbosity(log, verbose.HasValue());

                // Create a temporary folder for caching files during the operation.
                using (var cache = new LocalCache())
                {
                    // Load settings and file system.
                    var settings = LocalSettings.Load(optionConfigFile.Value(), SettingsUtility.GetPropertyMappings(propertyOptions.Values.ToList()));
                    var fileSystem = await Util.CreateFileSystemOrThrow(settings, sourceName.Value(), cache, log);

                    var success = await FeedSettingsCommand.RunAsync(
                        settings,
                        fileSystem,
                        unsetAll.HasValue(),
                        getAll.HasValue(),
                        getSetting.Values,
                        unset.Values,
                        setSetting.Values,
                        log,
                        CancellationToken.None);

                    return success ? 0 : 1;
                }
            });
        }
    }
}
