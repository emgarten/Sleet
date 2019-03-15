using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// Destroy and rebuild the feed.
    /// </summary>
    public static class RecreateCommand
    {
        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, string tmpPath, bool force, ILogger log)
        {
            var success = true;
            var cleanNupkgs = true;

            var token = CancellationToken.None;
            LocalCache localCache = null;

            // Use the tmp path if provided
            if (string.IsNullOrEmpty(tmpPath))
            {
                localCache = new LocalCache();
            }
            else
            {
                localCache = new LocalCache(tmpPath);
            }

            log.LogMinimal($"Reading feed {source.BaseURI.AbsoluteUri}");

            using (var feedLock = await SourceUtility.VerifyInitAndLock(settings, source, "Recreate", log, token))
            {
                if (!force)
                {
                    // Validate source
                    await UpgradeUtility.EnsureFeedVersionMatchesTool(source, allowNewer: true, log: log, token: token);
                }

                // Read settings and persist them in the new feed.
                var feedSettings = await FeedSettingsUtility.GetSettingsOrDefault(source, log, token);

                try
                {
                    var downloadSuccess = await DownloadCommand.DownloadPackages(settings, source, localCache.Root.FullName, force, log, token);

                    if (!force && !downloadSuccess)
                    {
                        log.LogError("Unable to recreate the feed due to errors download packages. Use --force to skip this check.");
                        return false;
                    }

                    var destroySuccess = await DestroyCommand.Destroy(settings, source, log, token);

                    if (!force && !destroySuccess)
                    {
                        log.LogError("Unable to completely remove the old feed before recreating. Use --force to skip this check.");
                        return false;
                    }

                    var initSuccess = await InitCommand.InitAsync(settings, source, feedSettings, log, token);

                    if (!initSuccess)
                    {
                        cleanNupkgs = false;

                        log.LogError("Unable to initialize the new feed. The feed is currently broken and must be repaired manually.");
                        success = false;
                        return false;
                    }

                    // Skip pushing for empty feeds
                    if (Directory.GetFiles(localCache.Root.FullName, "*.*", SearchOption.AllDirectories).Length > 0)
                    {
                        var pushSuccess = await PushCommand.PushPackages(settings, source, new List<string>() { localCache.Root.FullName }, force: true, skipExisting: true, log: log, token: token);

                        if (!pushSuccess)
                        {
                            cleanNupkgs = false;

                            log.LogError("Unable to push packages to the new feed. Try pushing the nupkgs again manually.");
                            success = false;
                            return false;
                        }
                    }

                    var validateSuccess = await ValidateCommand.Validate(settings, source, log, token);

                    if (!validateSuccess)
                    {
                        cleanNupkgs = false;

                        log.LogError("Something went wrong when recreating the feed. Feed validation has failed.");
                        success = false;
                        return false;
                    }

                }
                finally
                {
                    if (cleanNupkgs)
                    {
                        // Delete downloaded nupkgs
                        log.LogInformation($"Removing local nupkgs from {localCache.Root.FullName}");
                        localCache.Dispose();
                    }
                    else
                    {
                        var message = $"Encountered an error while recreating the feed. You may need to manually create the feed and push the nupkgs again. Nupkgs have been saved to: {localCache.Root.FullName}";

                        if (force)
                        {
                            log.LogWarning(message);
                        }
                        else
                        {
                            log.LogError(message);
                        }
                    }
                }

                log.LogMinimal("Feed recreation complete.");
            }

            return success;
        }
    }
}
