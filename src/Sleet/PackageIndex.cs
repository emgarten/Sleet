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
    // TODO: Optimize json reading/writing
    /// <summary>
    /// sleet.packageindex.json is a simple json index of all ids and versions contained in the feed.
    /// </summary>
    public class PackageIndex : ISleetService
    {
        private readonly SleetContext _context;

        public PackageIndex(SleetContext context)
        {
            _context = context;
        }

        public async Task AddPackage(PackageInput packageInput)
        {
            // Load existing index
            var index = await GetPackages();

            // Add package
            HashSet<NuGetVersion> versions;
            if (!index.TryGetValue(packageInput.Identity.Id, out versions))
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

        public async Task<bool> RemovePackage(PackageIdentity package)
        {
            // Load existing index
            var index = await GetPackages();

            // Remove package
            HashSet<NuGetVersion> versions;
            if (index.TryGetValue(package.Id, out versions) && versions.Remove(package.Version))
            {
                // Create updated index
                var json = CreateJson(index);
                var file = Index;

                await file.Write(json, _context.Log, _context.Token);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns all packages in the feed.
        /// Id -> Version
        /// </summary>
        public async Task<Dictionary<string, HashSet<NuGetVersion>>> GetPackages()
        {
            var index = new Dictionary<string, HashSet<NuGetVersion>>(StringComparer.OrdinalIgnoreCase);

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

                    HashSet<NuGetVersion> packageVersions;
                    if (!index.TryGetValue(id, out packageVersions))
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
        public async Task<HashSet<NuGetVersion>> GetPackagesWithId(string packageId)
        {
            var index = await GetPackages();

            HashSet<NuGetVersion> versions;
            if (!index.TryGetValue(packageId, out versions))
            {
                versions = new HashSet<NuGetVersion>();
            }

            return versions;
        }

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

            var byId = await GetPackagesWithId(packageId);

            return byId.Contains(version);
        }

        public Task<bool> Exists(PackageIdentity package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            return Exists(package.Id, package.Version);
        }

        private ISleetFile Index
        {
            get
            {
                return _context.Source.Get("/sleet.packageindex.json");
            }
        }

        private async Task<JObject> GetJson()
        {
            var file = Index;

            return await file.GetJson(_context.Log, _context.Token);
        }

        private static JObject CreateJson(Dictionary<string, HashSet<NuGetVersion>> index)
        {
            var json = new JObject();

            var packages = new JObject();

            json.Add("packages", packages);

            foreach (var id in index.Keys.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                var versionArray = new JArray(index[id].Select(v => v.ToNormalizedString()));
                packages.Add(id, versionArray);
            }

            return json;
        }
    }
}
