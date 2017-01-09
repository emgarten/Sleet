using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Sleet
{
    internal static class CreateConfigAppCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("createconfig", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Create a new sleet.json config file.";

            var azure = cmd.Option("--azure", "Add a template entry for an azure storage feed.",
                CommandOptionType.NoValue);

            var folder = cmd.Option("--local", "Add a template entry for a local folder feed.",
                CommandOptionType.NoValue);

            var output = cmd.Option("--output", "Output path. If not specified the file will be created in the working directory.",
                CommandOptionType.SingleValue);

            cmd.HelpOption(Constants.HelpOption);

            cmd.OnExecute(async () =>
            {
                var outputPath = output.HasValue() ? output.Value() : null;

                var success = await CreateConfigCommand.RunAsync(azure.HasValue(), folder.HasValue(), outputPath, log);

                return success ? 0 : 1;
            });
        }
    }
}