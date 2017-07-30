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
    public class PackageIndexFile : ISleetService, IPackagesLookup
    {
        private readonly SleetContext _context;

        public string Name { get; }

        private ISleetFile Index { get; set; }

        public PackageIndexFile(SleetContext context, string path, string name)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            _context = context ?? throw new ArgumentNullException(nameof(context));
            Index = context.Source.Get(path);
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public async Task AddPackageAsync(PackageInput packageInput)
        {
            // Load existing index
            var index = await GetPackageIndex();

            // Add package
            if (!index.TryGetValue(packageInput.Identity.Id, out ISet<NuGetVersion> versions))
            {
                versions = new HashSet<NuGetVersion>();
                index.Add(packageInput.Identity.Id, versions);
            }

            versions.Add(packageInput.Identity.Version);

            // Create updated index
            var json = CreateJson(index);
            var file = Index;

            await file.Write(json, _context.Log, _context.Token);
        }

        public async Task RemovePackageAsync(PackageIdentity package)
        {
            // Load existing index
            var index = await GetPackageIndex();

            // Remove package
            if (index.TryGetValue(package.Id, out ISet<NuGetVersion> versions) && versions.Remove(package.Version))
            {
                // Create updated index
                var json = CreateJson(index);
                var file = Index;

                await file.Write(json, _context.Log, _context.Token);
            }
        }

        /// <summary>
        /// Creates a set of all indexed packages
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackagesAsync()
        {
            var result = new HashSet<PackageIdentity>();

            var packages = await GetPackageIndex();

            foreach (var pair in packages)
            {
                foreach (var version in pair.Value)
                {
                    result.Add(new PackageIdentity(pair.Key, version));
                }
            }

            return result;
        }

        /// <summary>
        /// Returns all packages in the feed.
        /// Id -> Version
        /// </summary>
        public async Task<IDictionary<string, ISet<NuGetVersion>>> GetPackageIndex()
        {
            var index = new Dictionary<string, ISet<NuGetVersion>>(StringComparer.OrdinalIgnoreCase);

            var json = await GetJson();

            var packagesNode = json["packages"] as JObject;

            if (packagesNode == null)
            {
                throw new InvalidDataException("Packages node missing from sleet.packageindex.json");
            }

            foreach (var property in packagesNode.Properties())
            {
                var versions = (JArray)property.Value;

                foreach (var versionEntry in versions)
                {
                    var packageVersion = NuGetVersion.Parse(versionEntry.ToObject<string>());
                    var id = property.Name;

                    if (!index.TryGetValue(id, out ISet<NuGetVersion> packageVersions))
                    {
                        packageVersions = new HashSet<NuGetVersion>();
                        index.Add(id, packageVersions);
                    }

                    packageVersions.Add(packageVersion);
                }
            }

            return index;
        }

        /// <summary>
        /// Find all versions of a package.
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackagesByIdAsync(string packageId)
        {
            var results = new HashSet<PackageIdentity>();
            var versions = await GetPackageVersions(packageId);

            foreach (var version in versions)
            {
                results.Add(new PackageIdentity(packageId, version));
            }

            return results;
        }

        /// <summary>
        /// Find all versions of a package.
        /// </summary>
        public async Task<ISet<NuGetVersion>> GetPackageVersions(string packageId)
        {
            var index = await GetPackageIndex();

            if (!index.TryGetValue(packageId, out ISet<NuGetVersion> versions))
            {
                versions = new HashSet<NuGetVersion>();
            }

            return versions;
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

            return Index.Write(json, _context.Log, _context.Token);
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
                    { "packages", new JObject() }
                };
        }

        private async Task<JObject> GetJson()
        {
            var file = Index;

            return await file.GetJson(_context.Log, _context.Token);
        }

        private static JObject CreateJson(IDictionary<string, ISet<NuGetVersion>> index)
        {
            var json = new JObject();

            var packages = new JObject();

            json.Add("packages", packages);

            foreach (var id in index.Keys.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                var versionArray = new JArray(index[id].OrderByDescending(v => v)
                    .Select(v => v.ToFullString()));

                if (versionArray.Count > 0)
                {
                    packages.Add(id, versionArray);
                }
            }

            return json;
        }
    }
}