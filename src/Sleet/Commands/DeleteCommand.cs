using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Logging;
using NuGet.Packaging.Core;

namespace Sleet
{
    internal static class DeleteCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("delete", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Delete a package or packages from a feed.";

            var optionConfigFile = cmd.Option("-c|--config", "sleet.json file to read sources and settings from.",
                CommandOptionType.SingleValue);

            var sourceName = cmd.Option("-s|--source", "Source name from sleet.json.",
                CommandOptionType.SingleValue);

            var packageId = cmd.Option("-i|--id", "Package id.",
                CommandOptionType.SingleValue);

            var version = cmd.Option("-v|--version", "Package version. If this is not specified all versions will be deleted.",
                CommandOptionType.SingleValue);

            cmd.HelpOption("-?|-h|--help");

            var required = new List<CommandOption>()
            {
                sourceName,
                packageId
            };

            cmd.OnExecute(async () =>
            {
                cmd.ShowRootCommandFullNameAndVersion();

                // Validate parameters
                foreach (var requiredOption in required)
                {
                    if (!requiredOption.HasValue())
                    {
                        throw new ArgumentException($"Missing required parameter --{requiredOption.LongName}.");
                    }
                }

                var settings = LocalSettings.Load(optionConfigFile.Value());

                using (var cache = new LocalCache())
                {
                    var fileSystem = FileSystemFactory.CreateFileSystem(settings, cache, sourceName.Value());

                    if (fileSystem == null)
                    {
                        throw new InvalidOperationException("Unable to find source. Verify that the --source parameter is correct and that sleet.json contains the named source.");
                    }

                    return await RunCore(settings, fileSystem, packageId.Value(), version.Value(), log);
                }
            });
        }

        public static Task<int> RunCore(LocalSettings settings, ISleetFileSystem source, string packageId, string version, ILogger log)
        {
            throw new NotImplementedException();
        }

        public static Task<bool> Delete(PackageIdentity packageIdentity, SleetContext context)
        {
            throw new NotImplementedException();
        }
    }
}
