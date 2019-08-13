using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Versioning;

namespace Sleet
{
    public static class UpgradeUtility
    {


        /// <summary>
        /// Currently supported capabilities. New optional features can be added here if they need to block older clients.
        /// This is an alternative to blocking all older versions of Sleet from a feed with requiredVersion.
        /// </summary>
        public static List<FeedCapability> SupportedFeedCapabilities = new List<FeedCapability>()
        {
            FeedCapability.Parse("schema:1.0.0")
        };


        /// <summary>
        /// Read the assembly version.
        /// </summary>
        public static async Task<FeedRequirements> GetFeedRequirementsAsync(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            var requirements = new FeedRequirements();

            // Read feed index.json data
            var indexPath = fileSystem.Get("index.json");
            var json = await indexPath.GetJson(log, token);

            var sleetVersion = json.GetValue("sleet:version")?.ToString();
            if (!string.IsNullOrEmpty(sleetVersion))
            {
                if (!SemanticVersion.TryParse(sleetVersion, out var version))
                {
                    throw new InvalidOperationException("Invalid sleet:version in index.json");
                }

                requirements.CreatorSleetVersion = version;
            }

            var requiredVersion = json.GetValue("sleet:requiredVersion")?.ToString();
            if (!string.IsNullOrEmpty(requiredVersion))
            {
                if (!VersionRange.TryParse(requiredVersion, out var range))
                {
                    throw new InvalidOperationException("Invalid sleet:requiredVersion in index.json");
                }

                requirements.RequiredVersion = range;
            }

            var requiredCaps = json.GetValue("sleet:capabilities")?.ToString();
            if (!string.IsNullOrEmpty(requiredCaps))
            {
                // Space delimited list of caps
                foreach (var s in requiredCaps.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    requirements.RequiredCapabilities.Add(FeedCapability.Parse(s));
                }
            }

            return requirements;
        }

        /// <summary>
        /// Throw if the tool versions does not match the feed.
        /// </summary>
        public static async Task EnsureCompatibility(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            var requirements = await GetFeedRequirementsAsync(fileSystem, log, token);
            AddDefaultCapabilities(requirements);

            var originalVersion = AssemblyVersionHelper.GetVersion();
            var assemblyVersion = new NuGetVersion(originalVersion.Major, originalVersion.Minor, originalVersion.Patch);

            // Check if a newer client is needed for the feed
            if (!requirements.RequiredVersion.Satisfies(assemblyVersion))
            {
                throw new InvalidOperationException($"{fileSystem.BaseURI} requires Sleet version: {requirements.RequiredVersion.PrettyPrint()}  Upgrade your Sleet client to work with this feed.");
            }

            var compareResult = CompareCapabilities(SupportedFeedCapabilities, requirements.RequiredCapabilities);
            if (compareResult < 0)
            {
                throw new InvalidOperationException($"{fileSystem.BaseURI} requires a newer version of Sleet. Upgrade your Sleet client to work with this feed.");
            }
            else if (compareResult > 0)
            {
                throw new InvalidOperationException($"{fileSystem.BaseURI} uses an older version of Sleet: {requirements.CreatorSleetVersion.ToNormalizedString()}. Upgrade the feed to {assemblyVersion} by running 'Sleet recreate' against this feed.");
            }
        }

        /// <summary>
        /// -1 if the client needs to upgrade (supported caps are lower)
        /// 0 if the client and feed can work together.
        /// 1 if the feed requires an upgrade due to being incompatible with the newer client
        /// </summary>
        private static int CompareCapabilities(List<FeedCapability> supportedCaps, List<FeedCapability> requiredCaps)
        {
            foreach (var cap in requiredCaps)
            {
                var supportedCap = supportedCaps.FirstOrDefault(e => StringComparer.OrdinalIgnoreCase.Equals(e.Name, cap.Name));

                if (supportedCap == null)
                {
                    // The feed is newer
                    return -1;
                }

                // Versions must be an exact match
                var result = VersionComparer.Compare(supportedCap.Version, cap.Version, VersionComparison.Version);

                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }

        /// <summary>
        /// Add default capabilities if none are provided.
        /// </summary>
        public static void AddDefaultCapabilities(FeedRequirements requirements)
        {
            // Infer default schema from Sleet 2.2.x if none is provided.
            if (requirements.RequiredCapabilities.Count < 1)
            {
                if (requirements.CreatorSleetVersion < new SemanticVersion(2, 2, 0))
                {
                    requirements.RequiredCapabilities.Add(FeedCapability.Parse("schema:0.1.0"));
                }
                else
                {
                    requirements.RequiredCapabilities.Add(FeedCapability.Parse("schema:1.0.0"));
                }
            }
        }
    }
}