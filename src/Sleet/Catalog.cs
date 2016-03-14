using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Sleet
{
    public class Catalog : ISleetService
    {
        private readonly SleetContext _context;

        public Catalog(SleetContext context)
        {
            _context = context;
        }

        public Task AddPackage(PackageInput packageInput)
        {
            // Create package details page

            // Add catalog page entry

            // Update packageInput catalog url

            throw new NotImplementedException();
        }

        public Task<bool> RemovePackage(PackageIdentity package)
        {
            throw new NotImplementedException();
        }

        public Task<List<CatalogEntry>> GetEntries()
        {
            throw new NotImplementedException();
        }

        public Task<List<CatalogEntry>> GetEntries(string packageId)
        {
            throw new NotImplementedException();
        }

        public Task<CatalogEntry> GetEntry(PackageIdentity packageIdentity)
        {
            throw new NotImplementedException();
        }

        public Task<bool> Exists(PackageIdentity packageIdentity)
        {
            throw new NotImplementedException();
        }

        public JObject CreatePackageDetails(PackageInput packageInput)
        {
            var now = packageInput.Now;
            var date = now.ToString("yyyy.MM.dd.HH.mm.ss");
            var package = packageInput.Package;
            var nuspec = XDocument.Load(package.GetNuspec());
            var nuspecReader = new NuspecReader(nuspec);

            var rootUri = new Uri($"{_context.Source.Root}catalog/data/{date}/{packageInput.Identity.Id.ToLowerInvariant()}.{packageInput.Identity.Version.ToNormalizedString().ToLowerInvariant()}.json");

            var json = JsonUtility.Create(rootUri, new List<string>() { "PackageDetails", "catalog:Permalink" });

            var context = JsonUtility.GetContext("Catalog");
            json.Add("@context", context);

            json.Add("id", packageInput.Identity.Id);
            json.Add("version", packageInput.Identity.Version.ToNormalizedString());
            json.Add("verbatimVersion", packageInput.Identity.Version.ToString());

            json.Add("created", now.GetDateString());
            json.Add("lastEdited", "0001-01-01T00:00:00Z");

            var copyProperties = new List<string>()
            {
                "authors",
                "copyright",
                "description",
                "iconUrl",
                "projectUrl",
                "licenseUrl"
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
                json.Add("minClientVersion", minVersion.ToNormalizedString());
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
                var groupNode = JsonUtility.Create(rootUri, $"frameworkassemblygroup/{groupTFM}".ToLowerInvariant(), "FrameworkAssemblyGroup");

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
                var groupNode = JsonUtility.Create(rootUri, $"dependencygroup/{groupTFM}".ToLowerInvariant(), "PackageDependencyGroup");

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
                        var packageNode = JsonUtility.Create(rootUri, $"dependencygroup/{groupTFM}/{depPackage.Id}".ToLowerInvariant(), "PackageDependency");
                        packageNode.Add("id", depPackage.Id);
                        packageNode.Add("range", depPackage.VersionRange.ToNormalizedString());

                        packageArray.Add(packageNode);
                    }
                }
            }

            json.Add("sleet:downloadUrl", packageInput.NupkgUri.AbsoluteUri);

            // TODO: add files
            // TODO: add sleet properties here such as username

            return JsonLDTokenComparer.Format(json);
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
