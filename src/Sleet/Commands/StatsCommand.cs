using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace Sleet
{
    internal static class StatsCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("stats", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Report feed statistics.";

            var optionConfigFile = cmd.Option("-c|--config", "sleet.json file to read sources and settings from.",
                CommandOptionType.SingleValue);

            var sourceName = cmd.Option("-s|--source", "Source name from sleet.json.",
                            CommandOptionType.SingleValue);

            cmd.HelpOption("-?|-h|--help");

            var required = new List<CommandOption>()
            {
                sourceName
            };

            cmd.OnExecute(async () =>
            {
                try
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

                        return await RunCore(settings, fileSystem, log);
                    }
                }
                catch (Exception ex)
                {
                    log.LogError(ex.Message);
                    log.LogDebug(ex.ToString());
                }

                return 1;
            });
        }

        public static async Task<int> RunCore(LocalSettings settings, ISleetFileSystem source, ILogger log)
        {
            var exitCode = 0;

            log.LogMinimal($"Stats for {source.BaseURI}");

            var token = CancellationToken.None;

            // Check if already initialized
            using (var feedLock = await SourceUtility.VerifyInitAndLock(source, log, token))
            {
                // Validate source
                await UpgradeUtility.UpgradeIfNeeded(source, log, token);

                // Get sleet.settings.json
                var sourceSettings = new SourceSettings();

                // Settings context used for all operations
                var context = new SleetContext()
                {
                    LocalSettings = settings,
                    SourceSettings = sourceSettings,
                    Log = log,
                    Source = source,
                    Token = token
                };

                var catalog = new Catalog(context);

                var existingEntries = await catalog.GetExistingPackagesIndex();
                var packages = existingEntries.Select(e => e.PackageIdentity).ToList();
                var uniqueIds = packages.Select(e => e.Id).Distinct(StringComparer.OrdinalIgnoreCase);

                var catalogEntries = await catalog.GetIndexEntries();

                log.LogMinimal($"Catalog entries: {catalogEntries.Count}");
                log.LogMinimal($"Packages: {existingEntries.Count}");
                log.LogMinimal($"Unique package ids: {uniqueIds.Count()}");
            }

            return exitCode;
        }
    }
}
