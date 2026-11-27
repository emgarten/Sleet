using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    /// <summary>
    /// Represents Add/Remove operations.
    /// This allows large operations to be done in a single batch call.
    /// </summary>
    public class SleetOperations
    {
        /// <summary>
        /// Packages to add.
        /// </summary>
        public List<PackageInput> ToAdd { get; set; } = null!;

        /// <summary>
        /// Packages to remove.
        /// </summary>
        public List<PackageInput> ToRemove { get; set; } = null!;

        /// <summary>
        /// Index before changes.
        /// </summary>
        public PackageSets OriginalIndex { get; set; } = null!;

        /// <summary>
        /// Index with changes applied.
        /// </summary>
        public PackageSets UpdatedIndex { get; set; } = null!;

        public SleetOperations()
        {
            // Empty, assign directly.
        }

        public SleetOperations(PackageSets originalIndex, PackageSets updatedIndex, List<PackageInput> toAdd, List<PackageInput> toRemove)
        {
            OriginalIndex = originalIndex ?? throw new ArgumentNullException(nameof(originalIndex));
            UpdatedIndex = updatedIndex ?? throw new ArgumentNullException(nameof(updatedIndex));
            ToAdd = toAdd ?? throw new ArgumentNullException(nameof(toAdd));
            ToRemove = toRemove ?? throw new ArgumentNullException(nameof(toRemove));
        }

        /// <summary>
        /// Package ids which have changes.
        /// Does not include symbols!
        /// </summary>
        public HashSet<string> GetChangedIds()
        {
            var removedPackages = OriginalIndex.Packages.Index.Except(UpdatedIndex.Packages.Index);
            var addedPackages = UpdatedIndex.Packages.Index.Except(OriginalIndex.Packages.Index);
            return new HashSet<string>(removedPackages.Concat(addedPackages).Select(e => e.Id), StringComparer.OrdinalIgnoreCase);
        }

        public static async Task<SleetOperations> CreateDeleteAsync(SleetContext context, IEnumerable<PackageIdentity> packagesToRemove)
        {
            var originalIndex = await GetPackageSets(context);
            return CreateDelete(originalIndex, packagesToRemove);
        }

        public static SleetOperations CreateDelete(PackageSets originalIndex, IEnumerable<PackageIdentity> packagesToRemove)
        {
            var toAdd = new List<PackageInput>();
            var toRemove = packagesToRemove.Select(e => PackageInput.CreateForDelete(e, isSymbols: false)).ToList();

            return Create(originalIndex, toAdd, toRemove);
        }

        public static SleetOperations Create(PackageSets originalIndex, List<PackageInput> toAdd, List<PackageInput> toRemove)
        {
            var updated = GetUpdatedIndex(toAdd, toRemove, originalIndex);
            return new SleetOperations(originalIndex, updated, toAdd, toRemove);
        }

        private static Task<PackageSets> GetPackageSets(SleetContext context)
        {
            var index = new PackageIndex(context);
            return index.GetPackageSetsAsync();
        }

        /// <summary>
        /// Create a new PackageSets with the inputs applied.
        /// Symbols packages are not supported and are ignored, the existing symbols set is kept as-is.
        /// </summary>
        private static PackageSets GetUpdatedIndex(List<PackageInput> toAdd, List<PackageInput> toRemove, PackageSets packageIndexBeforeChanges)
        {
            var updatedIndex = packageIndexBeforeChanges.Clone();

            foreach (var input in toRemove.Where(e => !e.IsSymbolsPackage))
            {
                updatedIndex.Packages.Index.Remove(input.Identity);
            }

            foreach (var input in toAdd.Where(e => !e.IsSymbolsPackage))
            {
                updatedIndex.Packages.Index.Add(input.Identity);
            }

            return updatedIndex;
        }
    }
}
