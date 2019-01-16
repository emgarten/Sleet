using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class PackageInput : IComparable<PackageInput>, IEquatable<PackageInput>
    {
        public string PackagePath { get; }

        public PackageIdentity Identity { get; }

        public NuspecReader Nuspec { get; }

        // Thehse fields are populated by other steps
        public Uri NupkgUri { get; set; }

        public JObject PackageDetails { get; set; }

        public Uri RegistrationUri { get; set; }

        /// <summary>
        /// True if the package is a .symbols.nupkg
        /// </summary>
        public bool IsSymbolsPackage { get; }

        public PackageInput(string packagePath, bool isSymbolsPackage, NuspecReader nuspecReader)
        {
            PackagePath = packagePath ?? throw new ArgumentNullException(nameof(packagePath));
            IsSymbolsPackage = isSymbolsPackage;
            Nuspec = nuspecReader ?? throw new ArgumentNullException(nameof(nuspecReader));
            Identity = nuspecReader.GetIdentity();
        }        

        /// <summary>
        /// Creates a new zip archive on each call. This must be disposed of.
        /// </summary>
        public ZipArchive CreateZip()
        {
            return new ZipArchive(File.OpenRead(PackagePath), ZipArchiveMode.Read, leaveOpen: false);
        }

        public override string ToString()
        {
            var s = $"{Identity.Id} {Identity.Version.ToFullVersionString()}";

            if (IsSymbolsPackage)
            {
                s += " (Symbols)";
            }

            return s;
        }

        // Order by identity, then by symbols package last.
        public int CompareTo(PackageInput other)
        {
            if (other == null)
            {
                return -1;
            }

            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            var x = PackageIdentityComparer.Default.Compare(Identity, other.Identity);

            if (x == 0)
            {
                if (IsSymbolsPackage == other.IsSymbolsPackage)
                {
                    x = 0;
                }
                else if (IsSymbolsPackage)
                {
                    x = -1;
                }
                else if (other.IsSymbolsPackage)
                {
                    x = 1;
                }
            }

            return x;
        }

        public bool Equals(PackageInput other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return IsSymbolsPackage == other.IsSymbolsPackage
                && PackageIdentityComparer.Default.Equals(Identity, other.Identity);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PackageInput);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(Identity);
            combiner.AddObject(IsSymbolsPackage);

            return combiner.CombinedHash;
        }

        public static bool operator ==(PackageInput left, PackageInput right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(PackageInput left, PackageInput right)
        {
            return !(left == right);
        }

        public static bool operator <(PackageInput left, PackageInput right)
        {
            return ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;
        }

        public static bool operator <=(PackageInput left, PackageInput right)
        {
            return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
        }

        public static bool operator >(PackageInput left, PackageInput right)
        {
            return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
        }

        public static bool operator >=(PackageInput left, PackageInput right)
        {
            return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
        }

        /// <summary>
        /// Create a package input from the given file path.
        /// </summary>
        public static PackageInput Create(string file)
        {
            PackageInput result = null;

            using (var zip = new ZipArchive(File.OpenRead(file), ZipArchiveMode.Read, leaveOpen: false))
            using (var reader = new PackageArchiveReader(file))
            {
                var isSymbolsPackage = SymbolsUtility.IsSymbolsPackage(zip, file);
                result = new PackageInput(file, isSymbolsPackage, reader.NuspecReader);
            }

            return result;
        }
    }
}