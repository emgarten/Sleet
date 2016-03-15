using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Sleet
{
    public class FlatContainer : ISleetService
    {
        private readonly SleetContext _context;

        public FlatContainer(SleetContext context)
        {
            _context = context;
        }

        public async Task AddPackage(PackageInput packageInput)
        {
            // Add nupkg
            var nupkgFile = _context.Source.Get(GetNupkgPath(packageInput.Identity));
            packageInput.FlatContainerFiles.Add(nupkgFile.Path);

            await nupkgFile.Write(File.OpenRead(packageInput.PackagePath), _context.Log, _context.Token);

            // Add zip files
            var nuspecFound = false;
            var nuspecPath = $"{packageInput.Identity.Id}.nuspec".ToLowerInvariant();

            foreach (var file in packageInput.Zip.Entries)
            {
                var path = file.FullName.ToLowerInvariant();

                // Skip OPC files
                if (path.StartsWith("package/")
                    || path.StartsWith("_rels/")
                    || path.StartsWith("[content_types].xml"))
                {
                    continue;
                }

                // Disallow packages containing files that will cause collisions
                if (path == "index.json"
                    || path.EndsWith(".nupkg"))
                {
                    throw new InvalidDataException($"nupkgs may not contain index.json or .nupkg files. Path: '{packageInput.PackagePath}'.");
                }

                if (path == nuspecPath)
                {
                    nuspecFound = true;
                }

                var entryFile = _context.Source.Get(GetZipFileUri(packageInput.Identity, file.FullName));
                packageInput.FlatContainerFiles.Add(entryFile.Path);

                using (var stream = file.Open())
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    await entryFile.Write(ms, _context.Log, _context.Token);
                }
            }

            if (!nuspecFound)
            {
                throw new InvalidDataException($"Unable to find '{nuspecPath}'. Path: '{packageInput.PackagePath}'.");
            }

            // Update index
            var indexFile = _context.Source.Get(GetIndexUri(packageInput.Identity.Id));

            var versions = await GetVersions(packageInput.Identity.Id);

            versions.Add(packageInput.Identity.Version);

            var indexJson = CreateIndex(versions);

            await indexFile.Write(indexJson, _context.Log, _context.Token);

            // Set nupkg url
            packageInput.NupkgUri = nupkgFile.Path;
        }

        public async Task<bool> RemovePackage(PackageIdentity package)
        {
            var nupkgFile = _context.Source.Get(GetNupkgPath(package));

            if (await nupkgFile.Exists(_context.Log, _context.Token))
            {
                using (var zip = new ZipArchive(await nupkgFile.GetStream(_context.Log, _context.Token)))
                {
                    // Delete all nupkg files
                    foreach (var entry in zip.Entries)
                    {
                        var file = _context.Source.Get(entry.FullName);
                        file.Delete(_context.Log, _context.Token);
                    }
                }

                // Delete nupkg
                nupkgFile.Delete(_context.Log, _context.Token);
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

            return true;
        }

        public Uri GetNupkgPath(PackageIdentity package)
        {
            var id = package.Id;
            var version = package.Version.ToNormalizedString();

            return _context.Source.GetPath($"/flatcontainer/{id}/{version}/{id}.{version}.nupkg".ToLowerInvariant());
        }

        public Uri GetIndexUri(string id)
        {
            return _context.Source.GetPath($"/flatcontainer/{id}/index.json".ToLowerInvariant());
        }

        public Uri GetZipFileUri(PackageIdentity package, string filePath)
        {
            var id = package.Id;
            var version = package.Version.ToNormalizedString();

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
                versionArray.Add(new JValue(version.ToNormalizedString()));
            }

            json.Add("versions", versionArray);

            return json;
        }
    }
}
