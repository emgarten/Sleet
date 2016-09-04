using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public static class SourceUtility
    {
        public static async Task<ISleetFileSystemLock> VerifyInitAndLock(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            ISleetFileSystemLock feedLock = null;

            try
            {
                // Validate source
                var exists = await fileSystem.Validate(log, token);

                if (!exists)
                {
                    throw new InvalidOperationException($"Unable to use feed.");
                }

                feedLock = fileSystem.CreateLock(log);
                var isLocked = await feedLock.GetLock(TimeSpan.Zero, token);

                if (!isLocked)
                {
                    throw new InvalidOperationException("Unable to obtain a lock on the feed.");
                }

                var indexPath = fileSystem.Get("index.json");

                if (!await indexPath.Exists(log, token))
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
    }
}