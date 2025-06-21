using System.Collections.Generic;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Sleet
{
    internal static class RetentionSettingsAppCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("settings", cmd => Run(cmd, log));
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Modify feed settings to automatically prune packages on push.";

            var optionConfigFile = cmd.Option(Constants.ConfigOption, Constants.ConfigDesc,
                CommandOptionType.SingleValue);

            var sourceName = cmd.Option(Constants.SourceOption, Constants.SourceDesc,
                CommandOptionType.SingleValue);

            var verbose = cmd.Option(Constants.VerboseOption, Constants.VerboseDesc, CommandOptionType.NoValue);

            cmd.HelpOption(Constants.HelpOption);

            var stableVersions = cmd.Option("--stable", "Number of stable versions per package id to retain.", CommandOptionType.SingleValue);
            var prereleaseVersions = cmd.Option("--prerelease", "Number of prerelease versions per package id to retain.", CommandOptionType.SingleValue);
            var releaseLabelsValue = cmd.Option("--release-labels", "Group prerelease packages by the first X release labels. Each group will be pruned to the prerelease max if applied. (optional)", CommandOptionType.SingleValue);
            var disableRetention = cmd.Option("--disable", "Disable package retention.", CommandOptionType.NoValue);
            var propertyOptions = cmd.Option(Constants.PropertyOption, Constants.PropertyDescription, CommandOptionType.MultipleValue);

            var required = new List<CommandOption>() { stableVersions, prereleaseVersions };

            cmd.OnExecuteAsync(async _ =>
            {
                // Validate parameters
                // Disable must be used by itself
                CmdUtils.VerifyMutallyExclusiveOptions(new[] { disableRetention }, new[] { stableVersions, prereleaseVersions });

                if (!disableRetention.HasValue())
                {
                    // If disable was not set, verify both version parameters were set
                    CmdUtils.VerifyRequiredOptions(required.ToArray());
                }

                // Init logger
                Util.SetVerbosity(log, verbose.HasValue());

                // Create a temporary folder for caching files during the operation.
                using (var cache = new LocalCache())
                {
                    // Load settings and file system.
                    var settings = LocalSettings.Load(optionConfigFile.Value(), SettingsUtility.GetPropertyMappings(propertyOptions.Values.ToList()));
                    var fileSystem = await Util.CreateFileSystemOrThrow(settings, sourceName.Value(), cache, log);

                    var success = false;

                    var stableVersionMax = stableVersions.HasValue() ? int.Parse(stableVersions.Value()) : -1;
                    var prereleaseVersionMax = prereleaseVersions.HasValue() ? int.Parse(prereleaseVersions.Value()) : -1;
                    int? releaseLabelsCount = null;

                    if (releaseLabelsValue.HasValue())
                    {
                        releaseLabelsCount = int.Parse(releaseLabelsValue.Value());
                    }

                    success = await RetentionSettingsCommand.RunAsync(settings, fileSystem, stableVersionMax, prereleaseVersionMax, releaseLabelsCount, disableRetention.HasValue(), log);

                    return success ? 0 : 1;
                }
            });
        }
    }
}
