using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Versioning;

namespace Sleet
{
    public static class UpgradeUtility
    {
        /// <summary>
        /// Override this to set the tool version instead of reading the assembly.
        /// </summary>
        public static SemanticVersion OverrideSleetVersion = null;

        /// <summary>
        /// Read the assembly version.
        /// </summary>
        public static async Task<SemanticVersion> GetSleetVersionAsync(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            // Check for override
            if (OverrideSleetVersion != null)
            {
                return OverrideSleetVersion;
            }

            var indexPath = fileSystem.Get("index.json");
            var json = await indexPath.GetJson(log, token);
            var sleetVersion = json.GetValue("sleet:version")?.ToString();
            if (!SemanticVersion.TryParse(sleetVersion, out var version))
            {
                throw new InvalidOperationException("Invalid sleet:version in index.json");
            }

            return version;
        }

        /// <summary>
        /// Throw if the tool versions does not match the feed.
        /// </summary>
        public static Task<bool> EnsureFeedVersionMatchesTool(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            return EnsureFeedVersionMatchesTool(fileSystem, allowNewer: false, log: log, token: token);
        }

        /// <summary>
        /// Throw if the tool versions does not match the feed.
        /// </summary>
        public static async Task<bool> EnsureFeedVersionMatchesTool(ISleetFileSystem fileSystem, bool allowNewer, ILogger log, CancellationToken token)
        {
            var sourceVersion = await GetSleetVersionAsync(fileSystem, log, token);

            var assemblyVersion = AssemblyVersionHelper.GetVersion();

            if (!allowNewer && sourceVersion < assemblyVersion)
            {
                throw new InvalidOperationException($"{fileSystem.BaseURI} uses an older version of Sleet: {sourceVersion}. Upgrade the feed to {assemblyVersion} by running 'Sleet recreate' against this feed.");
            }
            else if (sourceVersion > assemblyVersion)
            {
                throw new InvalidOperationException($"{fileSystem.BaseURI} was created using a newer version of Sleet: {sourceVersion}. Use the same or higher version to make changes.");
            }

            return true;
        }
    }
}