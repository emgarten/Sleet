using System;

namespace Sleet
{
    /// <summary>
    /// Contains an asset path and the package index used for reverse look ups.
    /// </summary>
    public class AssetIndexEntry : IEquatable<AssetIndexEntry>, IComparable<AssetIndexEntry>
    {
        /// <summary>
        /// Asset path, this could be a dll or pdb.
        /// </summary>
        public Uri Asset { get; }

        /// <summary>
        /// Package index used for mapping the asset to packages.
        /// </summary>
        public Uri PackageIndex { get; }

        public AssetIndexEntry(Uri asset, Uri packageIndex)
        {
            Asset = asset ?? throw new ArgumentNullException(nameof(asset));
            PackageIndex = packageIndex ?? throw new ArgumentNullException(nameof(packageIndex));
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AssetIndexEntry);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.GetHashCode(Asset, PackageIndex);
        }

        public bool Equals(AssetIndexEntry other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Asset.Equals(other.Asset)
                && PackageIndex.Equals(other.PackageIndex);
        }

        public int CompareTo(AssetIndexEntry other)
        {
            return StringComparer.Ordinal.Compare(Asset.AbsoluteUri, other.Asset.AbsoluteUri);
        }

        public override string ToString()
        {
            return Asset.AbsoluteUri;
        }
    }
}
