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

        public async Task AddPackage(PackageInput package)
        {
            // Retrieve index
            var rootUri = GetIndexUri(package.Identity);
            var rootFile = _context.Source.Get(rootUri);

            var packages = new List<JObject>();

            if (await rootFile.Exists(_context.Log, _context.Token))
            {
                var json = await rootFile.GetJson(_context.Log, _context.Token);

                // Get all entries
                packages = await GetPackageDetails(json);
            }

            // Add entry
            var newEntry = await CreateItem(package);
            var removed = packages.RemoveAll(p => GetPackageVersion(p) == package.Identity.Version);

            if (removed > 0)
            {
                _context.Log.LogWarning($"Removed duplicate registration entry for: {package.Identity}");
            }

            packages.Add(newEntry);

            // Create index
            var newIndexJson = CreateIndex(rootUri, packages);

            // Write
            await rootFile.Write(newIndexJson, _context.Log, _context.Token);

            // Create package page
            var packageUri = GetPackageUri(package.Identity);
            var packageFile = _context.Source.Get(packageUri);

            var packageJson = await CreatePackageBlob(package);

            // Write package page
            await packageFile.Write(packageJson, _context.Log, _context.Token);
        }

        public async Task RemovePackage(PackageIdentity package)
        {
            var found = false;

            // Retrieve index
            var rootUri = GetIndexUri(package);
            var rootFile = _context.Source.Get(rootUri);

            var packages = new List<JObject>();

            if (await rootFile.Exists(_context.Log, _context.Token))
            {
                var json = await rootFile.GetJson(_context.Log, _context.Token);

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

                if (await packageFile.Exists(_context.Log, _context.Token))
                {
                    packageFile.Delete(_context.Log, _context.Token);
                }

                if (packages.Count > 0)
                {
                    // Create index
                    var newIndexJson = CreateIndex(rootUri, packages);

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

        public JObject CreateIndex(Uri indexUri, List<JObject> packageDetails)
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

            var context = JsonUtility.GetContext("Registration");
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
            var items = json["items"] as JArray;

            if (items != null)
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
            return new Uri($"{_context.Source.Root}registation/{package.Id.ToLowerInvariant()}/index.json");
        }

        public static Uri GetIndexUri(Uri sourceRoot, string packageId)
        {
            return new Uri($"{sourceRoot.AbsoluteUri}registation/{packageId.ToLowerInvariant()}/index.json");
        }

        public Uri GetPackageUri(PackageIdentity package)
        {
            return new Uri($"{_context.Source.Root}registation/{package.Id.ToLowerInvariant()}/{package.Version.ToIdentityString().ToLowerInvariant()}.json");
        }

        public static Uri GetPackageUri(Uri sourceRoot, PackageIdentity package)
        {
            return new Uri($"{sourceRoot.AbsoluteUri}registation/{package.Id.ToLowerInvariant()}/{package.Version.ToIdentityString().ToLowerInvariant()}.json");
        }

        public async Task<JObject> CreatePackageBlob(PackageInput packageInput)
        {
            var rootUri = GetPackageUri(packageInput.Identity);

            var json = JsonUtility.Create(rootUri, new string[] { "Package", "http://schema.nuget.org/catalog#Permalink" });

            var packageDetailsFile = _context.Source.Get(packageInput.PackageDetailsUri);

            if (!await packageDetailsFile.Exists(_context.Log, _context.Token))
            {
                throw new FileNotFoundException($"Unable to find {packageDetailsFile.Path.AbsoluteUri}");
            }

            var detailsJson = await packageDetailsFile.GetJson(_context.Log, _context.Token);

            json.Add("catalogEntry", packageInput.PackageDetailsUri.AbsoluteUri);
            json.Add("packageContent", detailsJson["packageContent"].ToString());
            json.Add("registration", GetIndexUri(packageInput.Identity));

            var copyProperties = new List<string>()
            {
                "listed",
                "published",
            };

            JsonUtility.CopyProperties(detailsJson, json, copyProperties, skipEmpty: true);

            var context = JsonUtility.GetContext("Package");
            json.Add("@context", context);

            return JsonLDTokenComparer.Format(json);
        }

        /// <summary>
        /// Create a package item entry.
        /// </summary>
        public async Task<JObject> CreateItem(PackageInput packageInput)
        {
            var rootUri = GetPackageUri(packageInput.Identity);

            var json = JsonUtility.Create(rootUri, "Package");
            json.Add("commitId", _context.CommitId.ToString().ToLowerInvariant());
            json.Add("commitTimeStamp", DateTimeOffset.UtcNow.GetDateString());

            var packageDetailsFile = _context.Source.Get(packageInput.PackageDetailsUri);
            var detailsJson = await packageDetailsFile.GetJson(_context.Log, _context.Token);

            json.Add("packageContent", detailsJson["packageContent"].ToString());
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
                "requiredLicenseAcceptance",
                "summary",
                "tags",
                "title",
                "version"
            };

            var catalogEntry = new JObject();

            JsonUtility.CopyProperties(detailsJson, catalogEntry, copyProperties, skipEmpty: true);

            json.Add("catalogEntry", catalogEntry);

            return JsonLDTokenComparer.Format(json);
        }

        /// <summary>
        /// Find all versions of a package.
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackagesById(string packageId)
        {
            var results = new HashSet<PackageIdentity>();

            // Retrieve index
            var rootUri = GetIndexUri(_context.Source.Root, packageId);
            var rootFile = _context.Source.Get(rootUri);

            var packages = new List<JObject>();

            if (await rootFile.Exists(_context.Log, _context.Token))
            {
                var json = await rootFile.GetJson(_context.Log, _context.Token);

                // Get all entries
                packages = await GetPackageDetails(json);

                var versions = packages.Select(GetPackageVersion);

                foreach (var version in versions)
                {
                    results.Add(new PackageIdentity(packageId, version));
                }
            }

            return results;
        }
    }
}
