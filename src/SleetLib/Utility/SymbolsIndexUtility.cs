using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using NuGet.Packaging.Core;

namespace Sleet
{
    public static class SymbolsIndexUtility
    {
        public static readonly string PackageIndexPath = "symbols/packages/index.json";

        /// <summary>
        /// Symbols/packages/id/version/
        /// </summary>
        public static string GetPackageDirectory(PackageIdentity identity)
        {
            return $"/symbols/packages/{identity.Id}/{identity.Version.ToNormalizedString()}/".ToLowerInvariant();
        }

        /// <summary>
        /// Symbols/packages/id/version/package.json
        /// </summary>
        public static string GetPackageToAssemblyIndexPath(PackageIdentity identity)
        {
            return GetPackageDirectory(identity) + "index.json";
        }

        /// <summary>
        /// Symbols/packages/id/version/package.json
        /// </summary>
        public static string GetSymbolsPackageDetailsPath(PackageIdentity identity)
        {
            return GetPackageDirectory(identity) + "package.json";
        }

        /// <summary>
        /// Symbols/packages/id/version/package.json
        /// </summary>
        public static string GetSymbolsNupkgPath(PackageIdentity identity)
        {
            var fileName = GetSymbolsNupkgFileName(identity);

            return GetPackageDirectory(identity) + fileName;
        }

        /// <summary>
        /// Symbols/packages/id/version/package.json
        /// </summary>
        public static string GetSymbolsNupkgFileName(PackageIdentity identity)
        {
            return $"{identity.Id}.{identity.Version.ToNormalizedString()}.symbols.nupkg".ToLowerInvariant();
        }

        /// <summary>
        /// Symbols/packages/index.json
        /// </summary>
        public static string GetPackageIndexPath(PackageIdentity identity)
        {
            return $"/symbols/packages/index.json";
        }

        /// <summary>
        /// Root path of dll or pdb
        /// Symbols/file/hash/
        /// </summary>
        public static string GetAssemblyFileDirectory(string fileName, string hash)
        {
            var symbolsPath = SymbolsUtility.GetSymbolsServerDirectoryPath(fileName, hash);

            return $"/symbols/{symbolsPath}";
        }

        /// <summary>
        /// Path to dll or pdb
        /// Symbols/file/hash/file.dll
        /// </summary>
        public static string GetAssemblyFilePath(string fileName, string hash)
        {
            var symbolsPath = SymbolsUtility.GetSymbolsServerPath(fileName, hash);

            return $"/symbols/{symbolsPath}";
        }

        /// <summary>
        /// Package index for a dll or pdb.
        /// Symbols/file/hash/packages.json
        /// </summary>
        public static string GetAssemblyToPackageIndexPath(string fileName, string hash)
        {
            var symbolsPath = SymbolsUtility.GetSymbolsServerDirectoryPath(fileName, hash);

            return $"/symbols/{symbolsPath}packages.json";
        }
    }
}
