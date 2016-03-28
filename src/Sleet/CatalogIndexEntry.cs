using System;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Sleet
{
    /// <summary>
    /// Unique by Id/Version
    /// Sorts by commit time
    /// </summary>
    public class CatalogIndexEntry : IEquatable<CatalogIndexEntry>, IComparable<CatalogIndexEntry>
    {
        public Uri PackageDetailsUrl { get; }

        public string Id
        {
            get
            {
                return PackageIdentity.Id;
            }
        }

        public NuGetVersion Version
        {
            get
            {
                return PackageIdentity.Version;
            }
        }

        public DateTimeOffset CommitTime { get; }

        public SleetOperation Operation { get; }

        public PackageIdentity PackageIdentity { get; }

        public CatalogIndexEntry(Uri packageDetailsUrl, string id, NuGetVersion version, DateTimeOffset commitTime, SleetOperation operation)
        {
            PackageDetailsUrl = packageDetailsUrl;
            CommitTime = commitTime;
            Operation = operation;
            PackageIdentity = new PackageIdentity(id, version);
        }

        public override bool Equals(object obj) => Equals(obj as CatalogIndexEntry);

        public override int GetHashCode()
        {
            return PackageIdentity.GetHashCode();
        }

        public bool Equals(CatalogIndexEntry other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return PackageIdentity.Equals(other.PackageIdentity);
        }

        public int CompareTo(CatalogIndexEntry other)
        {
            if (other == null)
            {
                return 1;
            }

            return CommitTime.CompareTo(other.CommitTime);
        }

        public override string ToString()
        {
            return $"{Operation} {Id} {Version.ToFullVersionString()}";
        }
    }
}
