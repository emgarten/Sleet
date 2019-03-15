using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using Sleet;

namespace Sleet
{
    /// <summary>
    /// Delete a feed from a source.
    /// </summary>
    public static class DestroyCommand
    {
        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, ILogger log)
        {
            var token = CancellationToken.None;

            using (var feedLock = await SourceUtility.VerifyInitAndLock(settings, source, "Destroy", log, token))
            {
                return await Destroy(settings, source, log, token);
            }
        }

        /// <summary>
        /// Destroy the feed. This does not include locking.
        /// </summary>
        public static async Task<bool> Destroy(LocalSettings settings, ISleetFileSystem source, ILogger log, CancellationToken token)
        {
            log.LogMinimal($"Reading feed {source.BaseURI.AbsoluteUri}");

            var success = await source.Destroy(log, token);

            // Save all
            if (success)
            {
                log.LogMinimal($"Destroying feed {source.BaseURI.AbsoluteUri}");

                success &= await source.Commit(log, token);
            }

            if (success)
            {
                log.LogMinimal($"Successfully deleted all files from {source.BaseURI.AbsoluteUri}");
            }
            else
            {
                log.LogError($"Failed to destroy feed {source.BaseURI.AbsoluteUri}! Try deleting all files manually.");
            }

            return success;
        }
    }
}
