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
        public static readonly string PackageIndexPath = "symbols/packages/index.json";

        public static string GetIndexRootFolderPath(PackageIdentity identity)
        {
            return $"/symbols/packages/{identity.Id}/{identity.Version.ToNormalizedString()}/".ToLowerInvariant();
        }

        public static string GetAssemblyPackageIndexPath(PackageIdentity identity)
        {
            return GetIndexRootFolderPath(identity) + "package.json";
        }

        public static string GetSymbolsPackageDetailsPath(PackageIdentity identity)
        {
            return GetIndexRootFolderPath(identity) + "package.json";
        }

        public static string GetPackageIndexPath(PackageIdentity identity)
        {
            return $"/symbols/packages/index.json";
        }

        public static PackageIndexFile GetPackageIndexFile(SleetContext context, PackageIdentity identity)
        {
            return new PackageIndexFile(context, GetPackageIndexPath(identity));
        }

        /// <summary>
        /// Root path of dll or pdb
        /// </summary>
        public static string GetAssemblyFileDirectory(string fileName, string hash)
        {
            var symbolsPath = SymbolsUtility.GetSymbolsServerDirectoryPath(fileName, hash);

            return $"/symbols/{symbolsPath}";
        }

        /// <summary>
        /// Path to dll or pdb
        /// </summary>
        public static string GetAssemblyFilePath(string fileName, string hash)
        {
            var symbolsPath = SymbolsUtility.GetSymbolsServerPath(fileName, hash);

            return $"/symbols/{symbolsPath}";
        }

        /// <summary>
        /// Package index for a dll or pdb.
        /// </summary>
        public static string GetAssemblyPackageIndex(string fileName, string hash)
        {
            var symbolsPath = SymbolsUtility.GetSymbolsServerDirectoryPath(fileName, hash);

            return $"/symbols/{symbolsPath}packages.json";
        }
    }
}
