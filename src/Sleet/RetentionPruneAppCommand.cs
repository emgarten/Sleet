using System.Collections.Generic;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Sleet
{
    internal static class RetentionPruneAppCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("prune", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Prune packages.";

            var optionConfigFile = cmd.Option(Constants.ConfigOption, Constants.ConfigDesc,
                CommandOptionType.SingleValue);

            var sourceName = cmd.Option(Constants.SourceOption, Constants.SourceDesc,
                CommandOptionType.SingleValue);

            var verbose = cmd.Option(Constants.VerboseOption, Constants.VerboseDesc, CommandOptionType.NoValue);

            cmd.HelpOption(Constants.HelpOption);

            var stableVersions = cmd.Option("--stable", "Number of stable versions per package id. If not specified the feed settings will be used.", CommandOptionType.SingleValue);
            var prereleaseVersions = cmd.Option("--prerelease", "Number of prerelease versions per package id. If not specified the feed settings will be used.", CommandOptionType.SingleValue);
            var packageIds = cmd.Option("--package", "Prune only the given package id or wildcard. If not specified all package ids will be used.", CommandOptionType.MultipleValue);
            var dryRun = cmd.Option("--dry-run", "Print out all versions that would be deleted without actually removing them.", CommandOptionType.NoValue);
            var propertyOptions = cmd.Option(Constants.PropertyOption, Constants.PropertyDescription, CommandOptionType.MultipleValue);

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
                    var settings = LocalSettings.Load(optionConfigFile.Value(), SettingsUtility.GetPropertyMappings(propertyOptions.Values));
                    var fileSystem = await Util.CreateFileSystemOrThrow(settings, sourceName.Value(), cache);

                    var success = false;

                    var pruneContext = new RetentionPruneCommandContext()
                    {
                        DryRun = dryRun.HasValue(),
                        StableVersionMax = stableVersions.HasValue() ? (int?)int.Parse(stableVersions.Value()) : null,
                        PrereleaseVersionMax = prereleaseVersions.HasValue() ? (int?)int.Parse(prereleaseVersions.Value()) : null,
                    };

                    if (packageIds.HasValue())
                    {
                        pruneContext.PackageIds.UnionWith(packageIds.Values);
                    }

                    success = await RetentionPruneCommand.RunAsync(settings, fileSystem, pruneContext, log);

                    return success ? 0 : 1;
                }
            });
        }
    }
}
