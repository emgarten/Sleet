using System;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class PackageInput : IDisposable, IComparable<PackageInput>, IEquatable<PackageInput>
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public string PackagePath { get; }

        public ZipArchive Zip { get; set; }

        public PackageIdentity Identity { get; set; }

        public PackageArchiveReader Package { get; set; }

        // Thehse fields are populated by other steps
        public Uri NupkgUri { get; set; }

        public JObject PackageDetails { get; set; }

        public Uri RegistrationUri { get; set; }

        /// <summary>
        /// True if the package is a .symbols.nupkg
        /// </summary>
        public bool IsSymbolsPackage { get; }

        public PackageInput(string packagePath, PackageIdentity identity, bool isSymbolsPackage)
        {
            PackagePath = packagePath ?? throw new ArgumentNullException(nameof(packagePath));
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            IsSymbolsPackage = isSymbolsPackage;
        }

        /// <summary>
        /// Run a non-thread safe action on the zip or package reader.
        /// </summary>
        public async Task<T> RunWithLockAsync<T>(Func<PackageInput, Task<T>> action)
        {
            await _semaphore.WaitAsync();

            var result = default(T);

            try
            {
                result = await action(this);
            }
            finally
            {
                _semaphore.Release();
            }

            return result;
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

        public void Dispose()
        {
            Package?.Dispose();
            Package = null;

            Zip?.Dispose();
            Zip = null;

            _semaphore.Dispose();
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
    }
}