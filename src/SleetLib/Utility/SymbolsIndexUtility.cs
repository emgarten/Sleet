using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection.PortableExecutable;
using System.IO.Compression;
using System.Linq;
using System.IO;
using NuGet.Packaging.Core;

namespace Sleet
{
    public static class SymbolsIndexUtility
    {
        public static readonly string PackageIndexPath = "symbols/packages/packageindex.json";

        public static readonly string SymbolsPackageIndexPath = "symbols/packages/symbolspackageindex.json";

        public static string GetIndexRootFolderPath(PackageIdentity identity)
        {
            return $"/symbols/packages/{identity.Id}/{identity.Version.ToNormalizedString()}/".ToLowerInvariant();
        }

        public static string GetPackageIndexPath(PackageIdentity identity)
        {
            return GetIndexRootFolderPath(identity) + "package.json";
        }

        public static string GetSymbolsPackageIndexPath(PackageIdentity identity)
        {
            return GetIndexRootFolderPath(identity) + "symbolspackage.json";
        }

        public static PackageIndexFile GetPackageIndexFile(SleetContext context, PackageIdentity identity)
        {
            return new PackageIndexFile(context, GetPackageIndexPath(identity), "PackageIndexForNonSymbolsPackages");
        }

        public static PackageIndexFile GetSymbolsPackageIndexFile(SleetContext context, PackageIdentity identity)
        {
            return new PackageIndexFile(context, GetSymbolsPackageIndexPath(identity), "PackageIndexForNonSymbolsPackages");
        }
    }
}
