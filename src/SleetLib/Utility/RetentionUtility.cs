using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

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
        public static HashSet<PackageIdentity> GetPackagesToPrune(HashSet<PackageIdentity> feedPackages, HashSet<PackageIdentity> pinnedPackages, int stableVersionMax, int prereleaseVersionMax)
        {
            var toPrune = new HashSet<PackageIdentity>();

            // Sort the set of feed packages in descending order
            var sorted = feedPackages.ToArray();
            Array.Sort(sorted);
            Array.Reverse(sorted);

            var lastId = string.Empty;
            var stable = 0;
            var pre = 0;

            // Loop through the list of packages
            // When the version count has exceeded the max prune the package
            // Pinned packages increase the count but will not be pruned
            foreach (var package in sorted)
            {
                // When a new id is encoutered clear the counts
                if (!StringComparer.OrdinalIgnoreCase.Equals(package.Id, lastId))
                {
                    stable = 0;
                    pre = 0;
                    lastId = package.Id;
                }

                var prune = false;
                if (package.Version.IsPrerelease)
                {
                    pre++;
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
    }
}
