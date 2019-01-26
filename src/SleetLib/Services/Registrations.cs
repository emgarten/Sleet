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
    /// Package registrations are an index to the catalog.
    /// </summary>
    public class Registrations : ISleetService, IPackageIdLookup, IAddRemovePackages
    {
        private readonly SleetContext _context;

        public string Name { get; } = nameof(Registrations);

        public Registrations(SleetContext context)
        {
            _context = context;
        }

        public Task AddPackageAsync(PackageInput package)
        {
            return AddPackagesAsync(new[] { package });
        }

        public Task RemovePackageAsync(PackageIdentity package)
        {
            return RemovePackagesAsync(new[] { package });
        }

        private void DeletePackagePage(PackageIdentity package)
        {
            // Delete package page
            var packageUri = GetPackageUri(package);
            var packageFile = _context.Source.Get(packageUri);
            packageFile.Delete(_context.Log, _context.Token);
        }

        public Task AddPackagesAsync(IEnumerable<PackageInput> packageInputs)
        {
            var byId = SleetUtility.GetPackageSetsById(packageInputs, e => e.Identity.Id);
            var tasks = new List<Func<Task>>();

            // Create page details pages and index pages in parallel.
            tasks.AddRange(byId.Select(e => new Func<Task>(() => CreatePackageIndexAsync(e.Key, e.Value))));
            tasks.AddRange(packageInputs.Select(e => new Func<Task>(() => CreatePackagePageAsync(e))));

            return TaskUtils.RunAsync(tasks);
        }

        private async Task CreatePackageIndexAsync(string packageId, List<PackageInput> packageInputs)
        {
            // Retrieve index
            var rootUri = GetIndexUri(packageId);
            var rootFile = _context.Source.Get(rootUri);

            var packages = new List<JObject>();
            var json = await rootFile.GetJsonOrNull(_context.Log, _context.Token);

            if (json != null)
            {
                // Get all entries
                packages = await GetPackageDetails(json);
            }

            // Remove any duplicates from the file
            var newPackageVersions = new HashSet<NuGetVersion>(packageInputs.Select(e => e.Identity.Version));

            foreach (var existingPackage in packages.ToArray())
            {
                var existingVersion = GetPackageVersion(existingPackage);

                if (newPackageVersions.Contains(existingVersion))
                {
                    packages.Remove(existingPackage);
                    _context.Log.LogWarning($"Removed duplicate registration entry for: {new PackageIdentity(packageId, existingVersion)}");
                }
            }

            // Add package entries
            foreach (var package in packageInputs)
            {
                // Add entry
                var newEntry = CreateItem(package);
                packages.Add(newEntry);
            }

            // Create index
            var newIndexJson = await CreateIndexAsync(rootUri, packages);

            // Write
            await rootFile.Write(newIndexJson, _context.Log, _context.Token);
        }

        /// <summary>
        /// Create a package details page for a package id/version.
        /// </summary>
        private async Task CreatePackagePageAsync(PackageInput package)
        {
            // Create package page
            var packageUri = GetPackageUri(package.Identity);
            var packageFile = _context.Source.Get(packageUri);
            var packageJson = await CreatePackageBlobAsync(package);

            // Write package page
            await packageFile.Write(packageJson, _context.Log, _context.Token);
        }

        public Task RemovePackagesAsync(IEnumerable<PackageIdentity> packagesToDelete)
        {
            var byId = SleetUtility.GetPackageSetsById(packagesToDelete, e => e.Id);
            var tasks = new List<Func<Task>>();

            foreach (var pair in byId)
            {
                var packageId = pair.Key;
                var versions = new HashSet<NuGetVersion>(pair.Value.Select(e => e.Version));
                tasks.Add(new Func<Task>(() => RemovePackagesFromIndexAsync(packageId, versions)));
            }

            return TaskUtils.RunAsync(tasks);
        }

        /// <summary>
        /// Remove packages from index and remove details pages if they exist.
        /// </summary>
        private async Task RemovePackagesFromIndexAsync(string packageId, HashSet<NuGetVersion> versions)
        {
            // Retrieve index
            var rootUri = GetIndexUri(packageId);
            var rootFile = _context.Source.Get(rootUri);
            var modified = false;

            var packages = new List<JObject>();
            var json = await rootFile.GetJsonOrNull(_context.Log, _context.Token);

            if (json != null)
            {
                // Get all entries
                packages = await GetPackageDetails(json);

                foreach (var entry in packages.ToArray())
                {
                    var version = GetPackageVersion(entry);

                    if (versions.Contains(version))
                    {
                        modified = true;
                        packages.Remove(entry);

                        // delete details page
                        DeletePackagePage(new PackageIdentity(packageId, version));
                    }
                }
            }

            if (modified)
            {
                if (packages.Count > 0)
                {
                    // Create index
                    var newIndexJson = await CreateIndexAsync(rootUri, packages);

                    // Write
                    await rootFile.Write(newIndexJson, _context.Log, _context.Token);
                }
                else
                {
                    // This package id been completely removed
                    rootFile.Delete(_context.Log, _context.Token);
                }
            }
        }

        /// <summary>
        /// Get all package details from all pages
        /// </summary>
        public Task<List<JObject>> GetPackageDetails(JObject json)
        {
            var pages = GetItems(json);
            return Task.FromResult(pages.SelectMany(GetItems).ToList());
        }

        public async Task<JObject> CreateIndexAsync(Uri indexUri, List<JObject> packageDetails)
        {
            var json = JsonUtility.Create(indexUri,
                new string[] {
                    "catalog:CatalogRoot",
                    "PackageRegistration",
                    "catalog:Permalink"
                });

            json.Add("commitId", _context.CommitId.ToString().ToLowerInvariant());
            json.Add("commitTimeStamp", DateTimeOffset.UtcNow.GetDateString());

            var itemsArray = new JArray();
            json.Add("items", itemsArray);
            json.Add("count", 1);

            // Add everything to a single page
            var pageJson = CreatePage(indexUri, packageDetails);
            itemsArray.Add(pageJson);

            var context = await JsonUtility.GetContextAsync("Registration");
            json.Add("@context", context);

            // Avoid formatting all package details again since this file could be very large.
            return JsonLDTokenComparer.Format(json, recurse: false);
        }

        public JObject CreatePage(Uri indexUri, List<JObject> packageDetails)
        {
            var versionSet = new HashSet<NuGetVersion>(packageDetails.Select(GetPackageVersion));
            var lower = versionSet.Min().ToIdentityString().ToLowerInvariant();
            var upper = versionSet.Max().ToIdentityString().ToLowerInvariant();

            var json = JsonUtility.Create(indexUri, $"page/{lower}/{upper}", "catalog:CatalogPage");

            json.Add("commitId", _context.CommitId.ToString().ToLowerInvariant());
            json.Add("commitTimeStamp", DateTimeOffset.UtcNow.GetDateString());

            json.Add("count", packageDetails.Count);

            json.Add("parent", indexUri.AbsoluteUri);
            json.Add("lower", lower);
            json.Add("upper", upper);

            // Order and add all items
            var itemsArray = new JArray(packageDetails.OrderBy(GetPackageVersion));
            json.Add("items", itemsArray);

            return JsonLDTokenComparer.Format(json, recurse: false);
        }

        public static NuGetVersion GetPackageVersion(JObject packageDetails)
        {
            var catalogEntry = (JObject)packageDetails["catalogEntry"];
            var version = NuGetVersion.Parse(catalogEntry.Property("version").Value.ToString());

            return version;
        }

        /// <summary>
        /// Get items from a page or index page.
        /// </summary>
        public static List<JObject> GetItems(JObject json)
        {
            var result = new List<JObject>();

            if (json["items"] is JArray items)
            {
                foreach (var item in items)
                {
                    result.Add((JObject)item);
                }
            }

            return result;
        }

        public Uri GetIndexUri(PackageIdentity package)
        {
            return GetIndexUri(package.Id);
        }

        public Uri GetIndexUri(string packageId)
        {
            return UriUtility.CreateUri($"{_context.Source.BaseURI}registration/{packageId.ToLowerInvariant()}/index.json");
        }

        public static Uri GetIndexUri(Uri sourceRoot, string packageId)
        {
            return UriUtility.CreateUri($"{sourceRoot.AbsoluteUri}registration/{packageId.ToLowerInvariant()}/index.json");
        }

        public Uri GetPackageUri(PackageIdentity package)
        {
            return UriUtility.CreateUri($"{_context.Source.BaseURI}registration/{package.Id.ToLowerInvariant()}/{package.Version.ToIdentityString().ToLowerInvariant()}.json");
        }

        public static Uri GetPackageUri(Uri sourceRoot, PackageIdentity package)
        {
            return UriUtility.CreateUri($"{sourceRoot.AbsoluteUri}registration/{package.Id.ToLowerInvariant()}/{package.Version.ToIdentityString().ToLowerInvariant()}.json");
        }

        /// <summary>
        /// Retrieve the PackageDetails from a package blob.
        /// </summary>
        public async Task<JObject> GetCatalogEntryFromPackageBlob(PackageIdentity package)
        {
            var uri = GetPackageUri(package);

            var file = _context.Source.Get(uri);
            var json = await file.GetJsonOrNull(_context.Log, _context.Token);

            if (json != null)
            {
                return json["sleet:catalogEntry"] as JObject;
            }

            return null;
        }

        public async Task<JObject> CreatePackageBlobAsync(PackageInput packageInput)
        {
            var rootUri = GetPackageUri(packageInput.Identity);

            var json = JsonUtility.Create(rootUri, new string[] { "Package", "http://schema.nuget.org/catalog#Permalink" });

            json.Add("catalogEntry", packageInput.PackageDetails.GetIdUri().AbsoluteUri);
            json.Add("packageContent", packageInput.PackageDetails["packageContent"].ToString());
            json.Add("registration", GetIndexUri(packageInput.Identity));

            var copyProperties = new List<string>()
            {
                "listed",
                "published",
            };

            JsonUtility.CopyProperties(packageInput.PackageDetails, json, copyProperties, skipEmpty: true);

            // Copy the catalog entry into the package blob. This allows the feed to 
            // save this info even if the catalog is disabled.
            // Note that this is different from NuGet.org, so the sleet: namespace is used.
            var catalogEntry = (JObject)packageInput.PackageDetails.DeepClone();

            // Clear packageEntries, this can be very large in some cases.
            catalogEntry.Remove("packageEntries");

            json.Add("sleet:catalogEntry", catalogEntry);

            var context = await JsonUtility.GetContextAsync("Package");
            json.Add("@context", context);

            return JsonLDTokenComparer.Format(json);
        }

        /// <summary>
        /// Create a package item entry.
        /// </summary>
        public JObject CreateItem(PackageInput packageInput)
        {
            var rootUri = GetPackageUri(packageInput.Identity);

            var json = JsonUtility.Create(rootUri, "Package");
            json.Add("commitId", _context.CommitId.ToString().ToLowerInvariant());
            json.Add("commitTimeStamp", DateTimeOffset.UtcNow.GetDateString());

            json.Add("packageContent", packageInput.PackageDetails["packageContent"].ToString());
            json.Add("registration", GetIndexUri(packageInput.Identity));

            var copyProperties = new List<string>()
            {
                "@id",
                "@type",
                "authors",
                "dependencyGroups",
                "description",
                "iconUrl",
                "id",
                "language",
                "licenseUrl",
                "listed",
                "minClientVersion",
                "packageContent",
                "projectUrl",
                "published",
                "requireLicenseAcceptance",
                "summary",
                "tags",
                "title",
                "version"
            };

            var catalogEntry = new JObject();

            JsonUtility.CopyProperties(packageInput.PackageDetails, catalogEntry, copyProperties, skipEmpty: true);

            json.Add("catalogEntry", catalogEntry);

            // Format package details at creation time, and avoid doing it again later to improve perf.
            return JsonLDTokenComparer.Format(json);
        }

        /// <summary>
        /// Find all versions of a package.
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackagesByIdAsync(string packageId)
        {
            var results = new HashSet<PackageIdentity>();

            // Retrieve index
            var rootUri = GetIndexUri(_context.Source.BaseURI, packageId);
            var rootFile = _context.Source.Get(rootUri);
            var json = await rootFile.GetJsonOrNull(_context.Log, _context.Token);

            if (json != null)
            {
                // Get all entries
                var packages = await GetPackageDetails(json);

                var versions = packages.Select(GetPackageVersion);

                foreach (var version in versions)
                {
                    results.Add(new PackageIdentity(packageId, version));
                }
            }

            return results;
        }

        public Task FetchAsync()
        {
            // Nothing to do
            return Task.FromResult(true);
        }
    }
}