using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public static class SourceUtility
    {
        public static async Task<ISleetFileSystemLock> VerifyInitAndLock(LocalSettings settings, ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
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

                feedLock = fileSystem.CreateLock(log);
                var isLocked = await feedLock.GetLock(settings.FeedLockTimeout, token);

                if (!isLocked)
                {
                    throw new InvalidOperationException("Unable to obtain a lock on the feed.");
                }

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
    }
}