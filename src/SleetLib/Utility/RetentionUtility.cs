using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Sleet
{
    public static class RetentionUtility
    {
        public static async Task<HashSet<PackageIdentity>> ResolvePackageSets(PackageSets packageSets)
        {
            var set = new HashSet<PackageIdentity>();

            set.UnionWith(await packageSets.Packages.GetPackagesAsync());
            set.UnionWith(await packageSets.Symbols.GetPackagesAsync());

            return set;
        }

        /// <summary>
        /// Find the set of packages to prune for a feed.
        /// Feed package ids that should be checked. Any filtering should happen before this.
        /// </summary>
        public static HashSet<PackageIdentity> GetPackagesToPrune(HashSet<PackageIdentity> feedPackages, HashSet<PackageIdentity> pinnedPackages, int stableVersionMax, int prereleaseVersionMax, int? groupByUniqueReleaseLabelCount = null)
        {
            var toPrune = new HashSet<PackageIdentity>();

            // Sort the set of feed packages in descending order
            var sorted = feedPackages.ToArray();
            Array.Sort(sorted);
            Array.Reverse(sorted);

            var lastId = string.Empty;
            var stable = 0;
            var labelLookup = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Loop through the list of packages
            // When the version count has exceeded the max prune the package
            // Pinned packages increase the count but will not be pruned
            foreach (var package in sorted)
            {
                // When a new id is encoutered clear the counts
                if (!StringComparer.OrdinalIgnoreCase.Equals(package.Id, lastId))
                {
                    stable = 0;
                    labelLookup.Clear();
                    lastId = package.Id;
                }

                var prune = false;
                if (package.Version.IsPrerelease)
                {
                    // Split prerelease groups by the given number of release labels
                    // If the number of labels is 0 then this will always return string.Empty and have
                    // no impact since all prerelease packages will be in the same group.
                    var labelKey = GetReleaseLabelKey(package.Version, groupByUniqueReleaseLabelCount);
                    var pre = labelLookup.AddOrUpdate(labelKey, 1, (k, v) => v + 1);

                    prune = pre > prereleaseVersionMax;
                }
                else
                {
                    stable++;
                    prune = stable > stableVersionMax;
                }

                // Skip pinned packages when pruning
                if (prune && !pinnedPackages.Contains(package))
                {
                    toPrune.Add(package);
                }
            }

            return toPrune;
        }

        public static string GetReleaseLabelKey(NuGetVersion version, int? count)
        {
            if (count == null || count < 1)
            {
                return string.Empty;
            }

            return string.Join(".", version.ReleaseLabels.Take(count.Value)).ToLowerInvariant();
        }
    }
}
