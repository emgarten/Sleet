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
    public class Registrations : ISleetService, IPackageIdLookup
    {
        private readonly SleetContext _context;

        public string Name { get; } = nameof(Registrations);

        public Registrations(SleetContext context)
        {
            _context = context;
        }

        public async Task AddPackageAsync(PackageInput package)
        {
            // Retrieve index
            var rootUri = GetIndexUri(package.Identity);
            var rootFile = _context.Source.Get(rootUri);

            var packages = new List<JObject>();

            var json = await rootFile.GetJsonOrNull(_context.Log, _context.Token);

            if (json != null)
            {
                // Get all entries
                packages = await GetPackageDetails(json);
            }

            // Add entry
            var newEntry = CreateItem(package);
            var removed = packages.RemoveAll(p => GetPackageVersion(p) == package.Identity.Version);

            if (removed > 0)
            {
                _context.Log.LogWarning($"Removed duplicate registration entry for: {package.Identity}");
            }

            packages.Add(newEntry);

            // Create index
            var newIndexJson = await CreateIndexAsync(rootUri, packages);

            // Write
            await rootFile.Write(newIndexJson, _context.Log, _context.Token);

            // Create package page
            var packageUri = GetPackageUri(package.Identity);
            var packageFile = _context.Source.Get(packageUri);
            var packageJson = await CreatePackageBlobAsync(package);

            // Write package page
            await packageFile.Write(packageJson, _context.Log, _context.Token);
        }

        public async Task RemovePackageAsync(PackageIdentity package)
        {
            var found = false;

            // Retrieve index
            var rootUri = GetIndexUri(package);
            var rootFile = _context.Source.Get(rootUri);

            var packages = new List<JObject>();
            var json = await rootFile.GetJsonOrNull(_context.Log, _context.Token);

            if (json != null)
            {
                // Get all entries
                packages = await GetPackageDetails(json);

                foreach (var entry in packages.ToArray())
                {
                    var version = GetPackageVersion(entry);

                    if (version == package.Version)
                    {
                        found = true;
                        packages.Remove(entry);
                    }
                }
            }

            if (found)
            {
                // Delete package page
                var packageUri = GetPackageUri(package);
                var packageFile = _context.Source.Get(packageUri);
                packageFile.Delete(_context.Log, _context.Token);

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

            return JsonLDTokenComparer.Format(json);
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

            var itemsArray = new JArray();
            json.Add("items", itemsArray);

            // Order and add all items
            foreach (var entry in packageDetails.OrderBy(GetPackageVersion))
            {
                itemsArray.Add(entry);
            }

            return JsonLDTokenComparer.Format(json);
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
            var fileSystemBase = (FileSystemBase)_context.Source;
            var fragment = fileSystemBase == null ? _context.Source.BaseURI : UriUtility.GetPath(_context.Source.BaseURI, fileSystemBase.FeedSubPath);
            return UriUtility.CreateUri($"{fragment}registration/{package.Id.ToLowerInvariant()}/index.json");
        }

        public static Uri GetIndexUri(Uri sourceRoot, string packageId)
        {
            return UriUtility.CreateUri($"{sourceRoot.AbsoluteUri}registration/{packageId.ToLowerInvariant()}/index.json");
        }

        public Uri GetPackageUri(PackageIdentity package)
        {
            var fileSystemBase = (FileSystemBase)_context.Source;
            var fragment = fileSystemBase == null ? _context.Source.BaseURI : UriUtility.GetPath(_context.Source.BaseURI, fileSystemBase.FeedSubPath);
            return UriUtility.CreateUri($"{fragment}registration/{package.Id.ToLowerInvariant()}/{package.Version.ToIdentityString().ToLowerInvariant()}.json");
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