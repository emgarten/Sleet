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

        public Task ApplyChangesAsync(SleetChangeContext changeContext)
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
            tasks.AddRange(changeContext.ToAdd.Select(e => new Func<Task>(() => AddNupkgAsync(e))));

            // Rebuild index files as needed
            var rebuildIds = changeContext.GetChangedIds();

            tasks.AddRange(rebuildIds.Select(e => new Func<Task>(() => CreateIndexAsync(e, changeContext.UpdatedIndex.Packages))));

            // Run all tasks
            return TaskUtils.RunAsync(tasks);
        }

        //public Task AddPackageAsync(PackageInput packageInput)
        //{
        //    return AddPackagesAsync(new[] { packageInput });
        //}

        //public Task RemovePackageAsync(PackageIdentity package)
        //{
        //    return RemovePackagesAsync(new[] { package });
        //}

        //public Task RemovePackagesAsync(IEnumerable<PackageIdentity> packages)
        //{
        //    var byId = SleetUtility.GetPackageSetsById(packages, e => e.Id);
        //    var tasks = new List<Func<Task>>();

        //    foreach (var pair in byId)
        //    {
        //        foreach (var package in pair.Value)
        //        {
        //            DeleteNupkg(package);
        //        }

        //        tasks.Add(() => RemoveVersionsAsync(pair.Key, pair.Value.Select(e => e.Version)));
        //    }

        //    return TaskUtils.RunAsync(tasks);
        //}

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

        //public Task AddPackagesAsync(IEnumerable<PackageInput> packageInputs)
        //{
        //    var byId = SleetUtility.GetPackageSetsById(packageInputs, e => e.Identity.Id);
        //    var tasks = new List<Func<Task>>();

        //    foreach (var pair in byId)
        //    {
        //        tasks.Add(() => AddVersionsAsync(pair.Key, pair.Value.Select(e => e.Identity.Version)));
        //        tasks.AddRange(pair.Value.Select(packageInput => new Func<Task>(() => AddNupkgAsync(packageInput))));
        //    }

        //    return TaskUtils.RunAsync(tasks);
        //}

        //private async Task RemoveVersionsAsync(string id, IEnumerable<NuGetVersion> versions)
        //{
        //    // Update index
        //    var indexFile = _context.Source.Get(GetIndexUri(id));

        //    var indexVersions = await GetVersions(id);
        //    indexVersions.ExceptWith(versions);

        //    if (indexVersions.Count > 0)
        //    {
        //        var indexJson = CreateIndexJson(indexVersions);
        //        await indexFile.Write(indexJson, _context.Log, _context.Token);
        //    }
        //    else
        //    {
        //        indexFile.Delete(_context.Log, _context.Token);
        //    }
        //}

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
            var indexJson = CreateIndexJson(versions);
            await indexFile.Write(indexJson, _context.Log, _context.Token);
        }

        private async Task AddNupkgAsync(PackageInput packageInput)
        {
            // Add nupkg
            var nupkgFile = _context.Source.Get(GetNupkgPath(packageInput.Identity));

            await nupkgFile.Write(File.OpenRead(packageInput.PackagePath), _context.Log, _context.Token);

            // Add nuspec
            var nuspecPath = $"{packageInput.Identity.Id}.nuspec".ToLowerInvariant();

            using (var nuspecStream = packageInput.Nuspec.Xml.AsMemoryStreamAsync())
            {
                var entryFile = _context.Source.Get(GetZipFileUri(packageInput.Identity, nuspecPath));
                await entryFile.Write(nuspecStream, _context.Log, _context.Token);
            }
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

        public Task FetchAsync()
        {
            // Nothing to do
            return Task.FromResult(true);
        }
    }
}