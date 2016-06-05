using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace Sleet
{
    public static class PruneUtility
    {
        public static Task<List<PackageIdentity>> PruneForNewInput(List<PackageIdentity> currentPackages, List<PackageIdentity> pinned, int makeSpaceFor,  SleetContext context)
        {
            // Check if prune is enabled

            // Get count for each id
            // Skip packages that already exist
            // Skip pinned packages

            throw new NotImplementedException();
        }
    }
}
