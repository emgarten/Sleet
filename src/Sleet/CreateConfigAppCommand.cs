using McMaster.Extensions.CommandLineUtils;
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

            var awss3 = cmd.Option("--s3", "Add a template entry for an Amazon S3 storage feed.",
                CommandOptionType.NoValue);

            var minios3 = cmd.Option("--minio", "Add a template entry for an MinIO S3 storage feed.",
                CommandOptionType.NoValue);

            var azure = cmd.Option("--azure", "Add a template entry for an azure storage feed.",
                CommandOptionType.NoValue);

            var folder = cmd.Option("--local", "Add a template entry for a local folder feed.",
                CommandOptionType.NoValue);

            var output = cmd.Option("--output", "Output path. If not specified the file will be created in the working directory.",
                CommandOptionType.SingleValue);

            var verbose = cmd.Option(Constants.VerboseOption, Constants.VerboseDesc, CommandOptionType.NoValue);

            cmd.HelpOption(Constants.HelpOption);

            cmd.OnExecute(async () =>
            {
                // Init logger
                Util.SetVerbosity(log, verbose.HasValue());

                var outputPath = output.HasValue() ? output.Value() : null;

                var storageType = awss3.HasValue() ? FileSystemStorageType.S3 :
                    minios3.HasValue() ? FileSystemStorageType.MinioS3 :
                    azure.HasValue() ? FileSystemStorageType.Azure :
                    folder.HasValue() ? FileSystemStorageType.Local :
                    FileSystemStorageType.Unspecified;
                var success = await CreateConfigCommand.RunAsync(storageType, outputPath, log);

                return success ? 0 : 1;
            });
        }
    }
}