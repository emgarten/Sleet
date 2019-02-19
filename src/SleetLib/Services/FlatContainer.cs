using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public Task ApplyOperationsAsync(SleetOperations changeContext)
        {
            // Remove existing files, this will typically result in marking the files
            // as deleted in the virtual file system since they have not been
            // downloaded. It is a fast/safe operation that must be done first.
            foreach (var toRemove in changeContext.ToRemove)
            {
                if (!toRemove.IsSymbolsPackage)
                {
                    DeleteNupkg(toRemove.Identity);
                }
            }

            var tasks = new List<Func<Task>>();

            // Copy in nupkgs/nuspec files
            // Ignore symbols packages
            tasks.AddRange(changeContext.ToAdd.Where(e => !e.IsSymbolsPackage).Select(e => new Func<Task>(() => AddPackageAsync(e))));

            // Rebuild index files as needed
            var rebuildIds = changeContext.GetChangedIds();

            tasks.AddRange(rebuildIds.Select(e => new Func<Task>(() => CreateIndexAsync(e, changeContext.UpdatedIndex.Packages))));

            // Run all tasks
            return TaskUtils.RunAsync(tasks);
        }

        private void DeleteNupkg(PackageIdentity package)
        {
            // Delete nupkg
            var nupkgFile = _context.Source.Get(GetNupkgPath(package));
            nupkgFile.Delete(_context.Log, _context.Token);

            // Nuspec
            var nuspecPath = $"{package.Id}.nuspec".ToLowerInvariant();
            var nuspecFile = _context.Source.Get(GetZipFileUri(package, nuspecPath));
            nuspecFile.Delete(_context.Log, _context.Token);
        }

        public Uri GetNupkgPath(PackageIdentity package)
        {
            return GetNupkgPath(_context, package);
        }

        public static Uri GetNupkgPath(SleetContext context, PackageIdentity package)
        {
            var id = package.Id;
            var version = package.Version.ToIdentityString();

            return context.Source.GetPath($"/flatcontainer/{id}/{version}/{id}.{version}.nupkg".ToLowerInvariant());
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
            var json = await file.GetJsonOrNull(_context.Log, _context.Token);

            if (json?.Property("versions")?.Value is JArray versionArray)
            {
                results.UnionWith(versionArray.Select(s => NuGetVersion.Parse(s.ToString())));
            }

            return results;
        }

        public JObject CreateIndexJson(SortedSet<NuGetVersion> versions)
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

        public async Task<ISet<PackageIdentity>> GetPackagesByIdAsync(string packageId)
        {
            var results = new HashSet<PackageIdentity>();
            var versions = await GetVersions(packageId);

            foreach (var version in versions)
            {
                results.Add(new PackageIdentity(packageId, version));
            }

            return results;
        }

        public Task PreLoadAsync(SleetOperations operations)
        {
            return Task.FromResult(true);
        }

        private async Task CreateIndexAsync(string id, PackageSet packageSet)
        {
            // Get all versions
            var packages = await packageSet.GetPackagesByIdAsync(id);
            var versions = new SortedSet<NuGetVersion>(packages.Select(e => e.Version));

            await CreateIndexAsync(id, versions);
        }

        private async Task CreateIndexAsync(string id, SortedSet<NuGetVersion> versions)
        {
            // Update index
            var indexFile = _context.Source.Get(GetIndexUri(id));

            using (var timer = PerfEntryWrapper.CreateModifyTimer(indexFile, _context.PerfTracker))
            {
                var indexJson = CreateIndexJson(versions);
                await indexFile.Write(indexJson, _context.Log, _context.Token);
            }
        }

        private Task AddPackageAsync(PackageInput packageInput)
        {
            return Task.WhenAll(new[]
            {
                AddNuspecAsync(packageInput),
                AddNupkgAsync(packageInput)
            });
        }

        private Task AddNupkgAsync(PackageInput packageInput)
        {
            // Add the nupkg by linking it instead of copying it to the local cache.
            var nupkgFile = _context.Source.Get(GetNupkgPath(packageInput.Identity));
            nupkgFile.Link(packageInput.PackagePath, _context.Log, _context.Token);

            return Task.FromResult(true);
        }

        private async Task AddNuspecAsync(PackageInput packageInput)
        {
            // Add nuspec
            var nuspecPath = $"{packageInput.Identity.Id}.nuspec".ToLowerInvariant();
            var entryFile = _context.Source.Get(GetZipFileUri(packageInput.Identity, nuspecPath));

            using (var nuspecStream = packageInput.Nuspec.Xml.AsMemoryStream())
            {
                await entryFile.Write(nuspecStream, _context.Log, _context.Token);
            }
        }
    }
}