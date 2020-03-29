using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public static class PruneUtility
    {
        public static async Task<HashSet<PackageIdentity>> ResolvePackageSets(PackageSets packageSets)
        {
            var set = new HashSet<PackageIdentity>();

            set.UnionWith(await packageSets.Packages.GetPackagesAsync());
            set.UnionWith(await packageSets.Symbols.GetPackagesAsync());

            return set;
        }

        public static HashSet<PackageIdentity> GetPackagesToPrune(HashSet<PackageIdentity> feedPackages, HashSet<PackageIdentity> pinnedPackages, int stableVersionMax, int prereleaseVersionMax)
        {
            var toPrune = new HashSet<PackageIdentity>();

            return toPrune;
        }
    }
}
