using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Sleet
{
    /// <summary>
    /// PackageIndexFile is a simple json index of all ids and versions contained in the feed.
    /// </summary>
    public class PackageIndexFile : IAddRemovePackages, IPackagesLookup, ISymbolsAddRemovePackages, ISymbolsPackagesLookup
    {
        private readonly SleetContext _context;

        private ISleetFile File { get; set; }

        public PackageIndexFile(SleetContext context, string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            _context = context ?? throw new ArgumentNullException(nameof(context));
            File = context.Source.Get(path);
        }

        public async Task AddPackageAsync(PackageInput packageInput)
        {
            // Load existing index
            var sets = await GetPackageSetsAsync();

            // Add package
            await sets.Packages.AddPackageAsync(packageInput);

            // Write file
            await Save(sets);
        }

        public async Task RemovePackageAsync(PackageIdentity package)
        {
            // Load existing index
            var sets = await GetPackageSetsAsync();

            // Remove package
            if (sets.Packages.Index.Remove(package))
            {
                // Create updated index
                await Save(sets);
            }
        }

        public async Task AddSymbolsPackageAsync(PackageInput packageInput)
        {
            // Load existing index
            var sets = await GetPackageSetsAsync();

            // Add package
            await sets.Symbols.AddPackageAsync(packageInput);

            // Write file
            await Save(sets);
        }

        public async Task RemoveSymbolsPackageAsync(PackageIdentity package)
        {
            // Load existing index
            var sets = await GetPackageSetsAsync();

            // Remove package
            if (sets.Symbols.Index.Remove(package))
            {
                // Create updated index
                await Save(sets);
            }
        }

        /// <summary>
        /// Creates a set of all indexed packages
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackagesAsync()
        {
            var sets = await GetPackageSetsAsync();

            return sets.Packages.Index;
        }

        public async Task<ISet<PackageIdentity>> GetSymbolsPackagesAsync()
        {
            var sets = await GetPackageSetsAsync();

            return sets.Symbols.Index;
        }

        public async Task<ISet<PackageIdentity>> GetSymbolsPackagesByIdAsync(string packageId)
        {
            var packages = await GetSymbolsPackagesAsync();

            return GetSetForId(packageId, packages);
        }

        public async Task<ISet<NuGetVersion>> GetPackageVersions(string packageId)
        {
            var packages = await GetPackagesByIdAsync(packageId);
            return new SortedSet<NuGetVersion>(packages.Select(e => e.Version));
        }

        public async Task<ISet<NuGetVersion>> GetSymbolsPackageVersions(string packageId)
        {
            var packages = await GetSymbolsPackagesByIdAsync(packageId);
            return new SortedSet<NuGetVersion>(packages.Select(e => e.Version));
        }

        /// <summary>
        /// Returns all packages in the feed.
        /// Id -> Version
        /// </summary>
        private async Task<PackageSets> GetPackageSetsAsync()
        {
            var index = new PackageSets();

            if (await File.Exists(_context.Log, _context.Token))
            {
                var json = await GetJson();

                var packagesNode = json["packages"] as JObject;

                if (packagesNode == null)
                {
                    throw new InvalidDataException("Packages node missing from sleet.packageindex.json");
                }

                index.Packages = GetPackageSetFromJson(packagesNode);

                var symbolsNode = json["symbols"] as JObject;

                if (symbolsNode == null)
                {
                    throw new InvalidDataException("Symbols node missing from sleet.packageindex.json");
                }

                index.Symbols = GetPackageSetFromJson(symbolsNode);
            }

            return index;
        }

        private static PackageSet GetPackageSetFromJson(JObject packagesNode)
        {
            var result = new PackageSet();

            foreach (var property in packagesNode.Properties())
            {
                var versions = (JArray)property.Value;
                var id = property.Name;

                foreach (var versionEntry in versions)
                {
                    var packageVersion = NuGetVersion.Parse(versionEntry.ToObject<string>());
                    result.Index.Add(new PackageIdentity(id, packageVersion));
                }
            }

            return result;
        }

        /// <summary>
        /// Find all versions of a package.
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackagesByIdAsync(string packageId)
        {
            var sets = await GetPackageSetsAsync();

            return GetSetForId(packageId, sets.Packages.Index);
        }

        /// <summary>
        /// True if the package exists in the index.
        /// </summary>
        public async Task<bool> Exists(string packageId, NuGetVersion version)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            var byId = await GetPackagesByIdAsync(packageId);

            return byId.Contains(new PackageIdentity(packageId, version));
        }

        public Task<bool> Exists(PackageIdentity package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            return Exists(package.Id, package.Version);
        }

        /// <summary>
        /// Create a new file.
        /// </summary>
        public Task Init()
        {
            var json = GetEmptyJson(_context.OperationStart);

            return File.Write(json, _context.Log, _context.Token);
        }

        /// <summary>
        /// Empty json file.
        /// </summary>
        public static JObject GetEmptyJson(DateTimeOffset createdDate)
        {
            return new JObject
                {
                    { "created", new JValue(createdDate.GetDateString()) },
                    { "lastEdited", new JValue(createdDate.GetDateString()) },
                    { "packages", new JObject() },
                    { "symbols", new JObject() }
                };
        }

        private async Task<JObject> GetJson()
        {
            var file = File;

            return await file.GetJson(_context.Log, _context.Token);
        }

        private static JObject CreateJson(PackageSets index)
        {
            var json = new JObject
            {
                { "packages", CreatePackageSetJson(index.Packages) },
                { "symbols", CreatePackageSetJson(index.Symbols) }
            };

            return json;
        }

        private static JObject CreatePackageSetJson(PackageSet packages)
        {
            var json = new JObject();

            var groups = packages.Index.GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var group in groups.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
            {
                var versionArray = new JArray(group.OrderByDescending(e => e.Version)
                    .Select(e => e.Version.ToFullString()));

                if (versionArray.Count > 0)
                {
                    json.Add(group.Key, versionArray);
                }
            }

            return json;
        }

        private static SortedSet<PackageIdentity> GetSetForId(string packageId, IEnumerable<PackageIdentity> packages)
        {
            return new SortedSet<PackageIdentity>(packages.Where(e => StringComparer.OrdinalIgnoreCase.Equals(packageId, e.Id)));
        }

        private class PackageSets
        {
            public PackageSet Packages { get; set; } = new PackageSet();

            public PackageSet Symbols { get; set; } = new PackageSet();
        }

        private async Task Save(PackageSets index)
        {
            // Create updated index
            var json = CreateJson(index);
            var file = File;

            await file.Write(json, _context.Log, _context.Token);
        }
    }
}