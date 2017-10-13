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

            var originalVersion = AssemblyVersionHelper.GetVersion();
            var assemblyVersion = new NuGetVersion(originalVersion.Major, originalVersion.Minor, originalVersion.Patch);

            // Allow all X.Y.* versions to be used, patches should only contain bug fixes
            // no breaking changes or new features.
            var allowedRange = GetAllowedRange(sourceVersion, allowNewer);

            if (!allowedRange.Satisfies(assemblyVersion))
            {
                if (sourceVersion < assemblyVersion)
                {
                    throw new InvalidOperationException($"{fileSystem.BaseURI} uses an older version of Sleet: {sourceVersion}. Upgrade the feed to {assemblyVersion} by running 'Sleet recreate' against this feed.");
                }
                else if (sourceVersion > assemblyVersion)
                {
                    throw new InvalidOperationException($"{fileSystem.BaseURI} was created using a newer version of Sleet: {sourceVersion}. Use the same or higher version to make changes.");
                }
            }

            return true;
        }

        /// <summary>
        /// Get the range of tool versions that can work with the source.
        /// </summary>
        public static VersionRange GetAllowedRange(SemanticVersion sourceVersion, bool allowNewer)
        {
            var minVersion = new NuGetVersion(sourceVersion.Major, sourceVersion.Minor, 0);
            var maxVersion = allowNewer ? null : new NuGetVersion(sourceVersion.Major, sourceVersion.Minor + 1, 0);

            // Create a range that allows any patch version to be used.
            // If allowNewer is set then allow an open range.
            return new VersionRange(
                minVersion: minVersion,
                includeMinVersion: true,
                maxVersion: maxVersion,
                includeMaxVersion: false);
        }
    }
}