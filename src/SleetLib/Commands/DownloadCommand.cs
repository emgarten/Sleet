using NuGet.Common;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;
using NuGet.Packaging.Core;
using System.Linq;

namespace Sleet
{
    /// <summary>
    /// Download all packages from the feed to a folder.
    /// </summary>
    public static class DownloadCommand
    {
        private const int MaxThreads = 4;

        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, string outputPath, bool ignoreErrors, ILogger log)
        {
            var token = CancellationToken.None;

            // Check if already initialized
            using (var feedLock = await SourceUtility.VerifyInitAndLock(settings, source, log, token))
            {
                // Validate source
                await UpgradeUtility.EnsureFeedVersionMatchesTool(source, log, token);

                return await DownloadPackages(settings, source, outputPath, ignoreErrors, log, token);
            }
        }

        /// <summary>
        /// Download packages. This method does not lock the feed or verify the client version.
        /// </summary>
        public static async Task<bool> DownloadPackages(LocalSettings settings, ISleetFileSystem source, string outputPath, bool ignoreErrors, ILogger log, CancellationToken token)
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

            var tasks = new List<Task<bool>>(MaxThreads);
            var downloadSuccess = true;

            log.LogMinimal($"Downloading nupkgs to {outputPath}");

            foreach (var pair in packages)
            {
                if (tasks.Count >= MaxThreads)
                {
                    downloadSuccess &= await CompleteTask(tasks);
                }

                var package = pair.Key;
                var nupkgFile = pair.Value;

                var fileName = UriUtility.GetFileName(nupkgFile.EntityUri);

                // id/id.version.nupkg or id/id.version.symbols.nupkg
                var outputNupkgPath = Path.Combine(outputPath,
                    package.Id.ToLowerInvariant(),
                    fileName.ToLowerInvariant());

                log.LogInformation($"Downloading {outputNupkgPath}");

                tasks.Add(nupkgFile.CopyTo(outputNupkgPath, overwrite: true, log: log, token: token));
            }

            while (tasks.Count > 0)
            {
                downloadSuccess &= await CompleteTask(tasks);
            }

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

        private static async Task<bool> CompleteTask(List<Task<bool>> tasks)
        {
            var task = await Task.WhenAny(tasks);
            tasks.Remove(task);

            return await task;
        }
    }
}
