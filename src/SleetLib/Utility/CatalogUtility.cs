using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Sleet
{
    public static class CatalogUtility
    {
        /// <summary>
        /// Create PackageDetails for a delete
        /// </summary>
        public static async Task<JObject> CreateDeleteDetailsAsync(PackageIdentity package, string reason, Uri catalogBaseURI, Guid commitId)
        {
            var now = DateTimeOffset.UtcNow;
            var pageId = Guid.NewGuid().ToString().ToLowerInvariant();

            var rootUri = UriUtility.GetPath(catalogBaseURI, $"data/{pageId}.json");

            var json = JsonUtility.Create(rootUri, new List<string>() { "PackageDelete", "catalog:Permalink" });
            json.Add("commitId", commitId.ToString().ToLowerInvariant());
            json.Add("commitTimeStamp", now.GetDateString());
            json.Add("sleet:operation", "remove");

            var context = await JsonUtility.GetContextAsync("Catalog");
            json.Add("@context", context);

            json.Add("id", package.Id);
            json.Add("version", package.Version.ToFullVersionString());

            json.Add("created", DateTimeOffset.UtcNow.GetDateString());
            json.Add("sleet:removeReason", reason);

            json.Add("sleet:toolVersion", AssemblyVersionHelper.GetVersion().ToFullVersionString());

            return JsonLDTokenComparer.Format(json);
        }

        /// <summary>
        /// Catalog index page.
        /// </summary>
        public static async Task<JObject> CreateCatalogPageAsync(Uri indexUri, Uri rootUri, List<JObject> packageDetails, Guid commitId)
        {
            var json = JsonUtility.Create(rootUri, "CatalogPage");
            json.Add("commitId", commitId.ToString().ToLowerInvariant());
            json.Add("commitTimeStamp", DateTimeOffset.UtcNow.GetDateString());
            json.Add("count", packageDetails.Count);
            json.Add("parent", indexUri.AbsoluteUri);

            var itemArray = new JArray();
            json.Add("items", itemArray);

            foreach (var entry in packageDetails
                .OrderBy(e => e["commitTimeStamp"].ToObject<DateTimeOffset>())
                .ThenBy(e => e["@id"].ToString()))
            {
                itemArray.Add(entry);
            }

            var context = await JsonUtility.GetContextAsync("CatalogPage");
            json.Add("@context", context);

            return JsonLDTokenComparer.Format(json);
        }

        /// <summary>
        /// Create a PackageDetails page that contains all the package information.
        /// </summary>
        public static Task<JObject> CreatePackageDetailsAsync(PackageInput packageInput, Uri catalogBaseURI, Guid commitId, bool writeFileList)
        {
            var pageId = Guid.NewGuid().ToString().ToLowerInvariant();
            var rootUri = UriUtility.GetPath(catalogBaseURI, $"data/{pageId}.json");

            return CreatePackageDetailsWithExactUriAsync(packageInput, rootUri, commitId, writeFileList);
        }

        /// <summary>
        /// Create a PackageDetails page that contains all the package information and an exact uri.
        /// </summary>
        public static async Task<JObject> CreatePackageDetailsWithExactUriAsync(PackageInput packageInput, Uri detailsUri, Guid commitId, bool writeFileList)
        {
            var now = DateTimeOffset.UtcNow;
            var nuspecReader = packageInput.Nuspec;

            var json = JsonUtility.Create(detailsUri, new List<string>() { "PackageDetails", "catalog:Permalink" });
            json.Add("commitId", commitId.ToString().ToLowerInvariant());
            json.Add("commitTimeStamp", DateTimeOffset.UtcNow.GetDateString());
            json.Add("sleet:operation", "add");

            var context = await JsonUtility.GetContextAsync("Catalog");
            json.Add("@context", context);

            json.Add("id", packageInput.Identity.Id);
            json.Add("version", packageInput.Identity.Version.ToFullVersionString());

            json.Add("created", now.GetDateString());
            json.Add("lastEdited", "0001-01-01T00:00:00Z");

            var copyProperties = new List<string>()
            {
                "authors",
                "copyright",
                "description",
                "iconUrl",
                "projectUrl",
                "licenseUrl",
                "language",
                "summary",
                "owners",
                "releaseNotes"
            };

            foreach (var propertyName in copyProperties)
            {
                json.Add(CreateProperty(propertyName, propertyName, nuspecReader));
            }

            json.Add("isPrerelease", packageInput.Identity.Version.IsPrerelease);

            // Unused?
            json.Add("licenseNames", string.Empty);
            json.Add("licenseReportUrl", string.Empty);

            // All packages are listed
            json.Add("listed", true);

            var titleValue = GetEntry(nuspecReader, "title");
            if (!string.IsNullOrEmpty(titleValue))
            {
                json.Add("title", titleValue);
            }

            using (var stream = File.OpenRead(packageInput.PackagePath))
            {
                using (var sha512 = SHA512.Create())
                {
                    var packageHash = Convert.ToBase64String(sha512.ComputeHash(stream));

                    json.Add("packageHash", packageHash);
                    json.Add("packageHashAlgorithm", "SHA512");
                }

                json.Add("packageSize", stream.Length);
            }

            json.Add("published", now.GetDateString());
            json.Add("requireLicenseAcceptance", GetEntry(nuspecReader, "requireLicenseAcceptance").Equals("true", StringComparison.OrdinalIgnoreCase));

            var minVersion = nuspecReader.GetMinClientVersion();

            if (minVersion != null)
            {
                json.Add("minClientVersion", minVersion.ToIdentityString());
            }

            // Tags
            var tagSet = new HashSet<string>(GetEntry(nuspecReader, "tags").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            tagSet.Remove(string.Empty);
            var tagArray = new JArray(tagSet);
            json.Add("tags", tagArray);

            // Framework assemblies
            var fwrGroups = nuspecReader.GetFrameworkReferenceGroups();
            var fwrArray = new JArray();
            json.Add("frameworkAssemblyGroup", fwrArray);

            foreach (var group in fwrGroups.OrderBy(e => e.TargetFramework.GetShortFolderName(), StringComparer.OrdinalIgnoreCase))
            {
                var groupTFM = group.TargetFramework.GetShortFolderName().ToLowerInvariant();
                var groupNode = JsonUtility.Create(detailsUri, $"frameworkassemblygroup/{groupTFM}".ToLowerInvariant(), "FrameworkAssemblyGroup");

                // Leave the framework property out for the 'any' group
                if (!group.TargetFramework.IsAny)
                {
                    groupNode.Add("targetFramework", groupTFM);
                }

                fwrArray.Add(groupNode);

                if (group.Items.Any())
                {
                    var assemblyArray = new JArray();
                    groupNode.Add("assembly", assemblyArray);

                    foreach (var fwAssembly in group.Items.Distinct().OrderBy(e => e, StringComparer.OrdinalIgnoreCase))
                    {
                        assemblyArray.Add(fwAssembly);
                    }
                }
            }

            // Dependencies
            var dependencyGroups = nuspecReader.GetDependencyGroups();

            var depArray = new JArray();
            json.Add("dependencyGroups", depArray);

            foreach (var group in dependencyGroups.OrderBy(e => e.TargetFramework.GetShortFolderName(), StringComparer.OrdinalIgnoreCase))
            {
                var groupTFM = group.TargetFramework.GetShortFolderName().ToLowerInvariant();
                var groupNode = JsonUtility.Create(detailsUri, $"dependencygroup/{groupTFM}".ToLowerInvariant(), "PackageDependencyGroup");

                // Leave the framework property out for the 'any' group
                if (!group.TargetFramework.IsAny)
                {
                    groupNode.Add("targetFramework", groupTFM);
                }

                depArray.Add(groupNode);

                if (group.Packages.Any())
                {
                    var packageArray = new JArray();
                    groupNode.Add("dependencies", packageArray);

                    foreach (var depPackage in group.Packages.Distinct().OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
                    {
                        var packageNode = JsonUtility.Create(detailsUri, $"dependencygroup/{groupTFM}/{depPackage.Id}".ToLowerInvariant(), "PackageDependency");
                        packageNode.Add("id", depPackage.Id);
                        packageNode.Add("range", depPackage.VersionRange.ToNormalizedString());

                        packageArray.Add(packageNode);
                    }
                }
            }

            json.Add("packageContent", packageInput.NupkgUri.AbsoluteUri);

            if (writeFileList)
            {
                // Write out all files contained in the package
                var packageEntriesArray = new JArray();
                json.Add("packageEntries", packageEntriesArray);

                using (var zip = packageInput.CreateZip())
                {
                    AddZipEntry(zip, detailsUri, packageEntriesArray);
                }
            }

            json.Add("sleet:toolVersion", AssemblyVersionHelper.GetVersion().ToFullVersionString());

            return JsonLDTokenComparer.Format(json);
        }

        private static void AddZipEntry(ZipArchive zip, Uri detailsUri, JArray packageEntriesArray)
        {
            var packageEntryIndex = 0;

            // This method is called from RunWithLockAsync
            foreach (var entry in zip.Entries.OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase))
            {
                var fileEntry = JsonUtility.Create(detailsUri, $"packageEntry/{packageEntryIndex}", "packageEntry");
                fileEntry.Add("fullName", entry.FullName);
                fileEntry.Add("length", entry.Length);
                fileEntry.Add("lastWriteTime", entry.LastWriteTime.GetDateString());

                packageEntriesArray.Add(fileEntry);
                packageEntryIndex++;
            }
        }

        /// <summary>
        /// Create an entry for a package. Used on catalog pages.
        /// </summary>
        public static JObject CreatePageCommit(PackageIdentity package, Uri packageDetailsUri, Guid commitId, SleetOperation operation, string entryType)
        {
            var pageCommit = JsonUtility.Create(packageDetailsUri, entryType);
            pageCommit["commitId"] = commitId.ToString().ToLowerInvariant();
            pageCommit["commitTimeStamp"] = DateTimeOffset.UtcNow.GetDateString();
            pageCommit["nuget:id"] = package.Id;
            pageCommit["nuget:version"] = package.Version.ToFullVersionString();
            pageCommit["sleet:operation"] = operation.ToString().ToLowerInvariant();
            return pageCommit;
        }

        /// <summary>
        /// Create a new page entry in the catalog index.
        /// </summary>
        public static JObject CreateCatalogIndexPageEntry(Uri pageUri, Guid commitId)
        {
            var newPage = JsonUtility.Create(pageUri, "CatalogPage");
            newPage["commitId"] = commitId.ToString().ToLowerInvariant();
            newPage["commitTimeStamp"] = DateTimeOffset.UtcNow.GetDateString();
            newPage["count"] = 0;

            newPage = JsonLDTokenComparer.Format(newPage);
            return newPage;
        }

        /// <summary>
        /// Update the catalog page and index.json file.
        /// </summary>
        public static void UpdatePageIndex(JObject catalogIndexJson, JObject currentPageJson, Guid commitId)
        {
            var pages = JsonUtility.GetItems(catalogIndexJson);
            var currentPageUri = JsonUtility.GetIdUri(currentPageJson);
            var pageCommits = JsonUtility.GetItems(currentPageJson);

            var pageEntry = pages.Where(e => JsonUtility.GetIdUri(e).Equals(currentPageUri)).Single();
            pageEntry["commitId"] = commitId.ToString().ToLowerInvariant();
            pageEntry["commitTimeStamp"] = DateTimeOffset.UtcNow.GetDateString();
            pageEntry["count"] = pageCommits.Count;
            catalogIndexJson["count"] = pages.Count;
        }

        private static JProperty CreateProperty(string catalogName, string nuspecName, NuspecReader nuspec)
        {
            var value = GetEntry(nuspec, nuspecName);

            return new JProperty(catalogName, value);
        }

        private static string GetEntry(NuspecReader reader, string property)
        {
            return reader.GetMetadata()
                .Where(pair => StringComparer.OrdinalIgnoreCase.Equals(pair.Key, property))
                .FirstOrDefault()
                .Value ?? string.Empty;
        }
    }
}
