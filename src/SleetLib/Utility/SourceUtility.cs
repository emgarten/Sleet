using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public static class SourceUtility
    {
        public static Task<ISleetFileSystemLock> VerifyInitAndLock(LocalSettings settings, ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            return VerifyInitAndLock(settings, fileSystem, lockMessage: null, log: log, token: token);
        }

        public static async Task<ISleetFileSystemLock> VerifyInitAndLock(LocalSettings settings, ISleetFileSystem fileSystem, string lockMessage, ILogger log, CancellationToken token)
        {
            ISleetFileSystemLock feedLock = null;

            ValidateFileSystem(fileSystem);

            try
            {
                // Validate source
                var exists = await fileSystem.Validate(log, token);

                if (!exists)
                {
                    throw new InvalidOperationException($"Unable to use feed.");
                }

                var timer = Stopwatch.StartNew();
                feedLock = fileSystem.CreateLock(log);

                // Use the message from settings as an override if it exists.
                var lockInfoMessage = string.IsNullOrEmpty(settings.FeedLockMessage) ? lockMessage : settings.FeedLockMessage;

                // Get lock
                var isLocked = await feedLock.GetLock(settings.FeedLockTimeout, lockInfoMessage, token);

                if (!isLocked)
                {
                    throw new InvalidOperationException("Unable to obtain a lock on the feed.");
                }

                // Log perf
                timer.Stop();
                fileSystem.LocalCache.PerfTracker.Add(new PerfSummaryEntry(timer.Elapsed, "Obtained feed lock in {0}", TimeSpan.FromSeconds(30)));

                var indexPath = fileSystem.Get("index.json");

                if (!await indexPath.ExistsWithFetch(log, token))
                {
                    throw new InvalidOperationException($"{fileSystem.BaseURI} is missing sleet files. Use 'sleet.exe init' to create a new feed.");
                }
            }
            catch
            {
                if (feedLock != null)
                {
                    feedLock.Release();
                }

                throw;
            }

            return feedLock;
        }

        public static void ValidateFileSystem(ISleetFileSystem fileSystem)
        {
            if (!string.IsNullOrEmpty(fileSystem.FeedSubPath)
                && !fileSystem.BaseURI.AbsoluteUri.EndsWith($"/{fileSystem.FeedSubPath.TrimEnd(new char[] { '/', '\\' })}/"))
            {
                throw new ArgumentException("When using FeedSubPath the Path property must end with the sub path.");
            }
        }

        /// <summary>
        /// Read index.json to find the BaseURI the feed was initialized with.
        /// </summary>
        public static async Task<Uri> GetBaseUriFromFeed(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            var settingsFileName = "sleet.settings.json";

            var indexPath = fileSystem.Get("index.json");
            var json = await indexPath.GetJson(log, token);
            var settingsUrl = json.GetJObjectArray("resources").Select(e => e.GetString("@id"))
                .First(e => e != null && e.EndsWith(settingsFileName, StringComparison.Ordinal));

            // Get the base url
            settingsUrl = settingsUrl.Substring(0, settingsUrl.Length - settingsFileName.Length);

            return new Uri(settingsUrl);
        }

        /// <summary>
        /// Throw if index.json contains a different baseURI than the local settings.
        /// </summary>
        public static async Task EnsureBaseUriMatchesFeed(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            var expected = await GetBaseUriFromFeed(fileSystem, log, token);
            var actual = UriUtility.GetPathWithoutFile(fileSystem.Get("sleet.settings.json").EntityUri);

            // Feeds will typically be case sensitive, but this is only checking for obvious mismatches to notify the user of problems.
            if (!StringComparer.OrdinalIgnoreCase.Equals(actual.AbsoluteUri, expected.AbsoluteUri))
            {
                throw new InvalidDataException($"The path or baseURI set in sleet.json does not match the URIs found in index.json. To fix this update sleet.json with the correct settings, or recreate the feed to apply the new settings. Local settings: {actual.AbsoluteUri} Feed settings: {expected.AbsoluteUri}");
            }
        }

        /// <summary>
        /// Verify the feed works with the current client and settings.
        /// </summary>
        public static async Task ValidateFeedForClient(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            await UpgradeUtility.EnsureFeedVersionMatchesTool(fileSystem, log, token);
            await EnsureBaseUriMatchesFeed(fileSystem, log, token);
        }

        public static FileSystemStorageType GetFeedType(string s)
        {
            if (Enum.TryParse<FileSystemStorageType>(s, ignoreCase: true, result: out var value))
            {
                return value;
            }

            return FileSystemStorageType.Unspecified;
        }
    }
}