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

        /// <summary>
        /// Verify a feed is valid and lock it. This will not automatically create the feed or initialize a new feed.
        /// </summary>
        public static Task<ISleetFileSystemLock> VerifyInitAndLock(LocalSettings settings, ISleetFileSystem fileSystem, string lockMessage, ILogger log, CancellationToken token)
        {
            return InitAndLock(settings, fileSystem, lockMessage, autoCreateBucket: false, autoInit: false, log: log, token: token);
        }

        /// <summary>
        /// Ensure a feed is initialized. If the feed is not initialized it can be automatically created and initialized.
        /// Feeds that are not in a vaild state will fail, these must be manually fixed to avoid losing data.
        /// </summary>
        /// <param name="lockMessage">Optional message to display when the feed is locked.</param>
        /// <param name="autoCreateBucket">Automatically create the folder/container/bucket without files.</param>
        /// <param name="autoInit">Automatically initialize the files in the feed.</param>
        public static async Task<ISleetFileSystemLock> InitAndLock(LocalSettings settings, ISleetFileSystem fileSystem, string lockMessage, bool autoCreateBucket, bool autoInit, ILogger log, CancellationToken token)
        {
            ISleetFileSystemLock feedLock = null;

            // Validate URI path
            ValidateFileSystem(fileSystem);

            // Create the bucket if allowed or throw.
            await EnsureBucketOrThrow(fileSystem, autoCreateBucket, log, token);

            try
            {
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

                // Reset the file system to avoid using files retrieved before the lock, this would be unsafe
                fileSystem.Reset();

                await EnsureFeedIndexOrThrow(settings, fileSystem, autoInit, log, token);
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

        /// <summary>
        /// Verify the feed has been initialized. Initialize it or throw.
        /// </summary>
        public static async Task EnsureFeedIndexOrThrow(LocalSettings settings, ISleetFileSystem fileSystem, bool autoInit, ILogger log, CancellationToken token)
        {
            var indexPath = fileSystem.Get("index.json");
            var validInit = await indexPath.ExistsWithFetch(log, token);

            if (!validInit && autoInit)
            {
                validInit = await InitCommand.RunAsync(settings, fileSystem, log);
            }

            if (!validInit)
            {
                throw new InvalidOperationException($"{fileSystem.BaseURI} is missing sleet files. Use 'sleet.exe init' to create a new feed.");
            }
        }

        /// <summary>
        /// Verify the feed exists, create it, or throw.
        /// </summary>
        public static async Task EnsureBucketOrThrow(ISleetFileSystem fileSystem, bool autoCreateBucket, ILogger log, CancellationToken token)
        {
            if (autoCreateBucket)
            {
                // Check if the feed exists already
                var bucketExists = await fileSystem.HasBucket(log, token);
                if (!bucketExists)
                {
                    await fileSystem.CreateBucket(log, token);
                }
            }
            else
            {
                // Validate source, this will provide a filesystem specific error if the bucket does
                // not exist.
                var exists = await fileSystem.Validate(log, token);
                if (!exists)
                {
                    throw new InvalidOperationException($"Unable to use feed. Create the appropriate feed folder/container/bucket to continue.");
                }
            }
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
            await UpgradeUtility.EnsureCompatibility(fileSystem, log, token);
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