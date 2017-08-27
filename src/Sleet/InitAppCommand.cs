using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Sleet
{
    internal static class InitAppCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("init", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
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

            var required = new List<CommandOption>();

            cmd.OnExecute(async () =>
            {
                // Validate parameters
                Util.ValidateRequiredOptions(required);

                // Init logger
                Util.SetVerbosity(log, verbose.HasValue());

                // Create a temporary folder for caching files during the operation.
                using (var cache = new LocalCache())
                {
                    // Load settings and file system.
                    var settings = LocalSettings.Load(optionConfigFile.Value());
                    var fileSystem = Util.CreateFileSystemOrThrow(settings, sourceName.Value(), cache);

                    var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalogOption.HasValue(), enableSymbolsOption.HasValue(), log, CancellationToken.None);

                    return success ? 0 : 1;
                }
            });
        }
    }
}