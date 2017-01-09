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
    public class FlatContainer : ISleetService, IPackageIdLookup
    {
        private readonly SleetContext _context;

        public string Name { get; } = nameof(FlatContainer);

        public FlatContainer(SleetContext context)
        {
            _context = context;
        }

        public async Task AddPackage(PackageInput packageInput)
        {
            // Add nupkg
            var nupkgFile = _context.Source.Get(GetNupkgPath(packageInput.Identity));

            await nupkgFile.Write(File.OpenRead(packageInput.PackagePath), _context.Log, _context.Token);

            // Add nuspec
            var nuspecPath = $"{packageInput.Identity.Id}.nuspec".ToLowerInvariant();

            var nuspecEntry = packageInput.Zip.Entries
                .Where(entry => nuspecPath.Equals(nuspecPath, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            if (nuspecEntry == null)
            {
                throw new InvalidDataException($"Unable to find '{nuspecPath}'. Path: '{packageInput.PackagePath}'.");
            }

            var entryFile = _context.Source.Get(GetZipFileUri(packageInput.Identity, nuspecPath));

            using (var stream = nuspecEntry.Open())
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
                await entryFile.Write(ms, _context.Log, _context.Token);
            }

            // Update index
            var indexFile = _context.Source.Get(GetIndexUri(packageInput.Identity.Id));

            var versions = await GetVersions(packageInput.Identity.Id);

            versions.Add(packageInput.Identity.Version);

            var indexJson = CreateIndex(versions);

            await indexFile.Write(indexJson, _context.Log, _context.Token);

            // Set nupkg url
            packageInput.NupkgUri = nupkgFile.EntityUri;
        }

        public async Task RemovePackage(PackageIdentity package)
        {
            // Nupkg
            var nupkgFile = _context.Source.Get(GetNupkgPath(package));

            if (await nupkgFile.Exists(_context.Log, _context.Token))
            {
                // Delete nupkg
                nupkgFile.Delete(_context.Log, _context.Token);
            }

            // Nuspec
            var nuspecPath = $"{package.Id}.nuspec".ToLowerInvariant();
            var nuspecFile = _context.Source.Get(GetZipFileUri(package, nuspecPath));

            if (await nuspecFile.Exists(_context.Log, _context.Token))
            {
                nuspecFile.Delete(_context.Log, _context.Token);
            }

            // Update index
            var indexFile = _context.Source.Get(GetIndexUri(package.Id));

            var versions = await GetVersions(package.Id);

            if (versions.Remove(package.Version))
            {
                if (versions.Count > 0)
                {
                    var indexJson = CreateIndex(versions);

                    await indexFile.Write(indexJson, _context.Log, _context.Token);
                }
                else
                {
                    indexFile.Delete(_context.Log, _context.Token);
                }
            }
        }

        public Uri GetNupkgPath(PackageIdentity package)
        {
            var id = package.Id;
            var version = package.Version.ToIdentityString();

            return _context.Source.GetPath($"/flatcontainer/{id}/{version}/{id}.{version}.nupkg".ToLowerInvariant());
        }

        public Uri GetIndexUri(string id)
        {
            return _context.Source.GetPath($"/flatcontainer/{id}/index.json".ToLowerInvariant());
        }

        public Uri GetZipFileUri(PackageIdentity package, string filePath)
        {
            var id = package.Id;
            var version = package.Version.ToIdentityString();

            return _context.Source.GetPath($"/flatcontainer/{id}/{version}/{filePath}".ToLowerInvariant());
        }

        public async Task<SortedSet<NuGetVersion>> GetVersions(string id)
        {
            var results = new SortedSet<NuGetVersion>();

            var file = _context.Source.Get(GetIndexUri(id));

            if (await file.Exists(_context.Log, _context.Token))
            {
                var json = await file.GetJson(_context.Log, _context.Token);

                var versionArray = json?.Property("versions")?.Value as JArray;

                if (versionArray != null)
                {
                    results.UnionWith(versionArray.Select(s => NuGetVersion.Parse(s.ToString())));
                }
            }

            return results;
        }

        public JObject CreateIndex(SortedSet<NuGetVersion> versions)
        {
            var json = new JObject();

            var versionArray = new JArray();

            foreach (var version in versions)
            {
                versionArray.Add(new JValue(version.ToIdentityString()));
            }

            json.Add("versions", versionArray);

            return json;
        }

        public async Task<ISet<PackageIdentity>> GetPackagesById(string packageId)
        {
            var results = new HashSet<PackageIdentity>();
            var versions = await GetVersions(packageId);

            foreach (var version in versions)
            {
                results.Add(new PackageIdentity(packageId, version));
            }

            return results;
        }
    }
}