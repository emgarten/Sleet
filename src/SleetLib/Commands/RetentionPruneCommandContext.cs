using System;
using System.Collections.Generic;
using System.Text;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class RetentionPruneCommandContext
    {
        public bool DryRun { get; set; }

        public int? StableVersionMax { get; set; }

        public int? PrereleaseVersionMax { get; set; }

        public int? GroupByFirstPrereleaseLabelCount { get; set; }

        /// <summary>
        /// Filter to only certain package ids.
        /// If empty all packages are processed.
        /// </summary>
        public HashSet<string> PackageIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Packages that should not be removed.
        /// </summary>
        public HashSet<PackageIdentity> PinnedPackages { get; set; } = new HashSet<PackageIdentity>();
    }
}
