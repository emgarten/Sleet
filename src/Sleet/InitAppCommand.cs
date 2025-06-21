using System;
using System.Collections.Generic;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Sleet
{
    internal static class InitAppCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("init", cmd => Run(cmd, log));
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Initialize a new sleet feed.";

            var optionConfigFile = cmd.Option(Constants.ConfigOption, Constants.ConfigDesc,
                CommandOptionType.SingleValue);

            var sourceName = cmd.Option(Constants.SourceOption, Constants.SourceDesc,
                CommandOptionType.SingleValue);

            var verbose = cmd.Option(Constants.VerboseOption, Constants.VerboseDesc, CommandOptionType.NoValue);

            cmd.HelpOption(Constants.HelpOption);

            var enableCatalogOption = cmd.Option(Constants.EnableCatalogOption, Constants.EnableCatalogDesc, CommandOptionType.NoValue);
            var enableSymbolsOption = cmd.Option(Constants.EnableSymbolsFeedOption, Constants.EnableSymbolsFeedDesc, CommandOptionType.NoValue);
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

                    var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalogOption.HasValue(), enableSymbolsOption.HasValue(), log, CancellationToken.None);

                    return success ? 0 : 1;
                }
            });
        }
    }
}
