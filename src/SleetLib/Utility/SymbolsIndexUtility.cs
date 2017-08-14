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

        public static string GetIndexRootFolderPath(PackageIdentity identity)
        {
            return $"/symbols/packages/{identity.Id}/{identity.Version.ToNormalizedString()}/".ToLowerInvariant();
        }

        public static string GetPackageIndexPath(PackageIdentity identity)
        {
            return GetIndexRootFolderPath(identity) + "package.json";
        }

        public static PackageIndexFile GetPackageIndexFile(SleetContext context, PackageIdentity identity)
        {
            return new PackageIndexFile(context, GetPackageIndexPath(identity));
        }
    }
}
