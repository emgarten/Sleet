using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Sleet
{
    internal static class RetentionAppCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            var parentCommand = cmdApp.Command("retention", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);

            RetentionSettingsAppCommand.Register(parentCommand, log);
            RetentionPruneAppCommand.Register(parentCommand, log);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Package retention commands for pruning and limiting package versions.";

            cmd.HelpOption(Constants.HelpOption);

            cmd.OnExecute(() =>
            {
                cmd.ShowHelp();

                return 0;
            });
        }
    }
}
