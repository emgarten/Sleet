using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class SleetChangeContext
    {
        /// <summary>
        /// Packages to add.
        /// </summary>
        public List<PackageInput> ToAdd { get; set; }

        /// <summary>
        /// Packages to remove.
        /// </summary>
        public List<PackageInput> ToRemove { get; set; }

        /// <summary>
        /// Index before changes.
        /// </summary>
        public PackageSets OriginalIndex { get; set; }

        /// <summary>
        /// Index with changes applied.
        /// </summary>
        public PackageSets UpdatedIndex { get; set; }

        public SleetChangeContext()
        {
            // Empty, assign directly.
        }

        public SleetChangeContext(PackageSets originalIndex, PackageSets updatedIndex, List<PackageInput> toAdd, List<PackageInput> toRemove)
        {
            OriginalIndex = originalIndex ?? throw new ArgumentNullException(nameof(originalIndex));
            UpdatedIndex = updatedIndex ?? throw new ArgumentNullException(nameof(updatedIndex));
            ToAdd = toAdd ?? throw new ArgumentNullException(nameof(toAdd));
            ToRemove = toRemove ?? throw new ArgumentNullException(nameof(toRemove));
        }

        /// <summary>
        /// Package ids which have changes.
        /// </summary>
        public HashSet<string> GetChangedIds()
        {
            var removedPackages = OriginalIndex.Packages.Index.Except(UpdatedIndex.Packages.Index);
            var addedPackages = UpdatedIndex.Packages.Index.Except(OriginalIndex.Packages.Index);
            return new HashSet<string>(removedPackages.Concat(addedPackages).Select(e => e.Id), StringComparer.OrdinalIgnoreCase);
        }

        public static async Task<SleetChangeContext> CreateDeleteAsync(SleetContext context, IEnumerable<PackageIdentity> packagesToRemove)
        {
            var originalIndex = await GetPackageSets(context);
            return CreateDelete(originalIndex, packagesToRemove);
        }

        public static async Task<SleetChangeContext> CreateDeleteAsync(SleetContext context, IEnumerable<PackageIdentity> packagesToRemove, IEnumerable<PackageIdentity> symbolsPackagesToRemove)
        {
            var originalIndex = await GetPackageSets(context);
            return CreateDelete(originalIndex, packagesToRemove, symbolsPackagesToRemove);
        }

        public static SleetChangeContext CreateDelete(PackageSets originalIndex, IEnumerable<PackageIdentity> packagesToRemove)
        {
            return CreateDelete(originalIndex, packagesToRemove, new List<PackageIdentity>());
        }

        public static SleetChangeContext CreateDelete(PackageSets originalIndex, IEnumerable<PackageIdentity> packagesToRemove, IEnumerable<PackageIdentity> symbolsPackagesToRemove)
        {
            var toAdd = new List<PackageInput>();
            var toRemove = new List<PackageInput>();
            toRemove.AddRange(packagesToRemove.Select(e => PackageInput.CreateForDelete(e, isSymbols: false)));
            toRemove.AddRange(symbolsPackagesToRemove.Select(e => PackageInput.CreateForDelete(e, isSymbols: true)));

            return Create(originalIndex, toAdd, toRemove);
        }

        public static SleetChangeContext Create(PackageSets originalIndex, List<PackageInput> toAdd, List<PackageInput> toRemove)
        {
            var updated = GetUpdatedIndex(toAdd, toRemove, originalIndex);
            return new SleetChangeContext(originalIndex, updated, toAdd, toRemove);
        }

        private static Task<PackageSets> GetPackageSets(SleetContext context)
        {
            var index = new PackageIndex(context);
            return index.GetPackageSetsAsync();
        }

        /// <summary>
        /// Create a new PackageSets with the inputs applied.
        /// </summary>
        private static PackageSets GetUpdatedIndex(List<PackageInput> toAdd, List<PackageInput> toRemove, PackageSets packageIndexBeforeChanges)
        {
            var updatedIndex = packageIndexBeforeChanges.Clone();

            foreach (var input in toRemove)
            {
                var set = input.IsSymbolsPackage ? updatedIndex.Symbols : updatedIndex.Packages;
                set.Index.Remove(input.Identity);
            }

            foreach (var input in toAdd)
            {
                var set = input.IsSymbolsPackage ? updatedIndex.Symbols : updatedIndex.Packages;
                set.Index.Add(input.Identity);
            }

            return updatedIndex;
        }
    }
}
