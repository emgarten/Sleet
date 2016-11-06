using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Versioning;

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

            var reason = cmd.Option("-r|--reason", "Reason for deleting the package.", CommandOptionType.SingleValue);

            var force = cmd.Option("-f|--force", "Ignore missing packages.", CommandOptionType.NoValue);

            cmd.HelpOption("-?|-h|--help");

            var required = new List<CommandOption>()
            {
                sourceName,
                packageId
            };

            cmd.OnExecute(async () =>
            {
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

                    return await RunCore(settings, fileSystem, packageId.Value(), version.Value(), reason.Value(), force.HasValue(), log);
                }
            });
        }

        public static async Task<int> RunCore(LocalSettings settings, ISleetFileSystem source, string packageId, string version, string reason, bool force, ILogger log)
        {
            var exitCode = 0;

            var token = CancellationToken.None;
            var now = DateTimeOffset.UtcNow;

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

                var packageIndex = new PackageIndex(context);

                var packages = new List<PackageIdentity>();

                if (!string.IsNullOrEmpty(version))
                {
                    // Delete a single version of the package
                    var packageVersion = NuGetVersion.Parse(version);

                    packages.Add(new PackageIdentity(packageId, packageVersion));
                }
                else
                {
                    // Delete all versions of the package
                    packages.AddRange(await packageIndex.GetPackagesById(packageId));
                }

                if (string.IsNullOrEmpty(reason))
                {
                    reason = string.Empty;
                }

                foreach (var package in packages)
                {
                    if (!await packageIndex.Exists(package))
                    {
                        log.LogInformation($"{package.ToString()} does not exist.");

                        if (force)
                        {
                            // ignore failures
                            continue;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Package does not exists: {package.ToString()}");
                        }
                    }

                    log.LogInformation($"Removing {package.ToString()}");
                    await SleetUtility.RemovePackage(context, package);
                }

                // Save all
                await source.Commit(log, token);
            }

            return exitCode;
        }
    }

    public static class DeleteCommandTestHook
    {
        public static Task<int> RunCore(LocalSettings settings, ISleetFileSystem source, string packageId, string version, string reason, bool force, ILogger log)
        {
            return DeleteCommand.RunCore(settings, source, packageId, version, reason, force, log);
        }
    }
}