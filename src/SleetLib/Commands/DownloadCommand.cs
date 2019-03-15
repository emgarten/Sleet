using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace Sleet
{
    /// <summary>
    /// Download all packages from the feed to a folder.
    /// </summary>
    public static class DownloadCommand
    {
        private const int MaxThreads = 8;

        public static Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, string outputPath, bool ignoreErrors, ILogger log)
        {
            return RunAsync(settings, source, outputPath, ignoreErrors, noLock: false, skipExisting: false, log: log);
        }

        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, string outputPath, bool ignoreErrors, bool noLock, bool skipExisting, ILogger log)
        {
            var token = CancellationToken.None;
            ISleetFileSystemLock feedLock = null;
            var success = true;
            var perfTracker = source.LocalCache.PerfTracker;

            using (var timer = PerfEntryWrapper.CreateSummaryTimer("Total execution time: {0}", perfTracker))
            {
                // Check if already initialized
                try
                {
                    if (!noLock)
                    {
                        // Lock
                        feedLock = await SourceUtility.VerifyInitAndLock(settings, source, "Download", log, token);

                        // Validate source
                        await UpgradeUtility.EnsureFeedVersionMatchesTool(source, log, token);
                    }

                    success = await DownloadPackages(settings, source, outputPath, ignoreErrors, log, token);
                }
                finally
                {
                    feedLock?.Dispose();
                }
            }

            // Write out perf summary
            await perfTracker.LogSummary(log);
            return success;
        }


        /// <summary>
        /// Download packages. This method does not lock the feed or verify the client version.
        /// </summary>
        public static Task<bool> DownloadPackages(LocalSettings settings, ISleetFileSystem source, string outputPath, bool ignoreErrors, ILogger log, CancellationToken token)
        {
            return DownloadPackages(settings, source, outputPath, ignoreErrors, skipExisting: false, log: log, token: token);
        }

        /// <summary>
        /// Download packages. This method does not lock the feed or verify the client version.
        /// </summary>
        public static async Task<bool> DownloadPackages(LocalSettings settings, ISleetFileSystem source, string outputPath, bool ignoreErrors, bool skipExisting, ILogger log, CancellationToken token)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                throw new ArgumentException("Missing output path parameter!");
            }

            var success = true;

            // Get sleet.settings.json
            var sourceSettings = await FeedSettingsUtility.GetSettingsOrDefault(source, log, token);

            // Settings context used for all operations
            var context = new SleetContext()
            {
                LocalSettings = settings,
                SourceSettings = sourceSettings,
                Log = log,
                Source = source,
                Token = token
            };

            log.LogMinimal($"Reading feed {source.BaseURI.AbsoluteUri}");

            // Find all packages
            var packageIndex = new PackageIndex(context);
            var flatContainer = new FlatContainer(context);
            var symbols = new Symbols(context);

            // Discover all packages
            var packages = new List<KeyValuePair<PackageIdentity, ISleetFile>>();
            packages.AddRange((await packageIndex.GetPackagesAsync()).Select(e =>
                new KeyValuePair<PackageIdentity, ISleetFile>(e, context.Source.Get(flatContainer.GetNupkgPath(e)))));
            packages.AddRange((await packageIndex.GetSymbolsPackagesAsync()).Select(e =>
                new KeyValuePair<PackageIdentity, ISleetFile>(e, symbols.GetSymbolsNupkgFile(e))));

            log.LogMinimal($"Downloading nupkgs to {outputPath}");

            // Run downloads
            var tasks = packages.Select(e => new Func<Task<bool>>(() => DownloadPackageAsync(outputPath, skipExisting, log, e, token)));
            var results = await TaskUtils.RunAsync(tasks, useTaskRun: true, token: CancellationToken.None);

            var downloadSuccess = results.All(e => e);
            success &= downloadSuccess;

            if (packages.Count < 1)
            {
                log.LogWarning("The feed does not contain any packages.");
            }

            if (downloadSuccess)
            {
                if (packages.Count > 0)
                {
                    log.LogMinimal("Successfully downloaded packages.");
                }
            }
            else
            {
                var message = $"Failed to download all packages!";

                if (ignoreErrors)
                {
                    log.LogWarning(message);
                }
                else
                {
                    log.LogError(message);
                }
            }

            return success;
        }

        private static async Task<bool> DownloadPackageAsync(string outputPath, bool skipExisting, ILogger log, KeyValuePair<PackageIdentity, ISleetFile> pair, CancellationToken token)
        {
            var package = pair.Key;
            var nupkgFile = pair.Value;

            var fileName = UriUtility.GetFileName(nupkgFile.EntityUri);

            // id/id.version.nupkg or id/id.version.symbols.nupkg
            var outputNupkgPath = Path.Combine(outputPath,
                package.Id.ToLowerInvariant(),
                fileName.ToLowerInvariant());

            await log.LogAsync(LogLevel.Information, $"Downloading {outputNupkgPath}");
            return await nupkgFile.CopyTo(outputNupkgPath, overwrite: !skipExisting, log: log, token: token);
        }
    }
}
