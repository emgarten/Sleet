using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class Symbols : ISleetService, ISymbolsAddRemovePackages, ISymbolsPackagesLookup, IValidatableService, IPackagesLookup, IAddRemovePackages
    {
        private readonly SleetContext _context;

        public string Name => nameof(Symbols);

        public PackageIndexFile PackageIndex { get; }

        public Symbols(SleetContext context)
        {
            _context = context;
            PackageIndex = new PackageIndexFile(context, SymbolsIndexUtility.PackageIndexPath, persistWhenEmpty: true);
        }

        public Task AddPackageAsync(PackageInput packageInput)
        {
            return AddPackageAsync(packageInput, isSymbolsPackage: false);
        }

        private async Task AddPackageAsync(PackageInput packageInput, bool isSymbolsPackage)
        {
            var tasks = new List<Task>();

            if (isSymbolsPackage)
            {
                // Add symbols packages to the feed regardless of assemblies
                tasks.Add(AddSymbolsNupkgToFeed(packageInput));
            }

            // Read dll/pdb files from the package.
            var assemblies = await GetAssembliesAsync(packageInput);

            if (assemblies.Count > 0)
            {
                // Add the id/version to the package index.
                if (isSymbolsPackage)
                {
                    tasks.Add(PackageIndex.AddSymbolsPackageAsync(packageInput));
                }
                else
                {
                    tasks.Add(PackageIndex.AddPackageAsync(packageInput));
                }

                // Add dll/pdb files to the feed.
                tasks.AddRange(assemblies.Select(e => AddAssemblyAsync(e, packageInput)));

                // Add assembly -> package reverse lookup
                tasks.AddRange(assemblies.Select(e => AddAssemblyToPackageIndexAsync(packageInput, e.IndexFile, isSymbolsPackage: isSymbolsPackage)));

                // Add index of all dll/pdb files added for the package.
                tasks.Add(AddPackageToAssemblyIndexAsync(packageInput.Identity, assemblies, isSymbolsPackage: isSymbolsPackage));
            }
            else
            {
                await _context.Log.LogAsync(LogLevel.Verbose, $"No files found that could be added to the symbols feed. Skipping package {packageInput.Identity}");
            }

            // Wait for everything to finish
            await Task.WhenAll(tasks);

            // Dispose of memory streams
            assemblies.ForEach(e => e.Dispose());
        }

        public async Task RemovePackageAsync(PackageIdentity package)
        {
            if (await PackageIndex.Exists(package))
            {
                var toDelete = new List<ISleetFile>();

                // Remove package->assembly index
                // Indexes are auto removed when empty
                var packageToAssemblyIndex = GetPackageToAssetIndex(package);
                var assets = await packageToAssemblyIndex.GetAssetsAsync();

                foreach (var asset in assets)
                {
                    // Remove package entry from assets
                    var assetIndex = GetAssemblyToPackageIndex(asset.PackageIndex);
                    await assetIndex.RemovePackageAsync(package);

                    // Remove assemblies
                    if (await assetIndex.IsEmpty())
                    {
                        // If the no other packages are referencing the file then remove the assembly.
                        var assetFile = _context.Source.Get(asset.Asset);
                        toDelete.Add(assetFile);
                    }
                }

                // Remove asset from parent index
                await packageToAssemblyIndex.RemoveAssetsAsync(assets);

                // Remove from index
                await PackageIndex.RemovePackageAsync(package);

                foreach (var file in toDelete)
                {
                    file.Delete(_context.Log, _context.Token);
                }
            }
        }

        public async Task AddPackagesAsync(IEnumerable<PackageInput> packageInputs)
        {
            foreach (var packageInput in packageInputs)
            {
                await AddPackageAsync(packageInput);
            }
        }

        public async Task RemovePackagesAsync(IEnumerable<PackageIdentity> packages)
        {
            foreach (var package in packages)
            {
                await RemovePackageAsync(package);
            }
        }

        public Task AddSymbolsPackageAsync(PackageInput packageInput)
        {
            return AddPackageAsync(packageInput, isSymbolsPackage: true);
        }

        public async Task RemoveSymbolsPackageAsync(PackageIdentity package)
        {
            if (await PackageIndex.SymbolsExists(package))
            {
                var toDelete = new List<ISleetFile>();

                // Remove nupkg
                var packagePath = SymbolsIndexUtility.GetSymbolsNupkgPath(package);
                toDelete.Add(_context.Source.Get(packagePath));

                // Remove details page
                var detailsPath = SymbolsIndexUtility.GetSymbolsPackageDetailsPath(package);
                toDelete.Add(_context.Source.Get(detailsPath));

                // Remove package->assembly index
                // Indexes are auto removed when empty
                var packageToAssemblyIndex = GetPackageToAssetIndex(package);
                var assets = await packageToAssemblyIndex.GetSymbolsAssetsAsync();

                foreach (var asset in assets)
                {
                    // Remove package entry from assets
                    var assetIndex = GetAssemblyToPackageIndex(asset.PackageIndex);
                    await assetIndex.RemoveSymbolsPackageAsync(package);

                    // Remove assemblies
                    if (await assetIndex.IsEmpty())
                    {
                        // If the no other packages are referencing the file then remove the assembly.
                        var assetFile = _context.Source.Get(asset.Asset);
                        toDelete.Add(assetFile);
                    }
                }

                // Remove asset from parent index
                await packageToAssemblyIndex.RemoveSymbolsAssetsAsync(assets);

                // Remove from index
                await PackageIndex.RemoveSymbolsPackageAsync(package);

                foreach (var file in toDelete)
                {
                    file.Delete(_context.Log, _context.Token);
                }
            }
        }

        public Task<ISet<PackageIdentity>> GetPackagesAsync()
        {
            return PackageIndex.GetPackagesAsync();
        }

        public Task<ISet<PackageIdentity>> GetPackagesByIdAsync(string packageId)
        {
            return PackageIndex.GetPackagesByIdAsync(packageId);
        }

        public Task<ISet<PackageIdentity>> GetSymbolsPackagesAsync()
        {
            return PackageIndex.GetSymbolsPackagesAsync();
        }

        public Task<ISet<PackageIdentity>> GetSymbolsPackagesByIdAsync(string packageId)
        {
            return PackageIndex.GetSymbolsPackagesByIdAsync(packageId);
        }

        private async Task AddAssemblyAsync(PackageFile assembly, PackageInput packageInput)
        {
            var file = _context.Source.Get(SymbolsIndexUtility.GetAssemblyFilePath(assembly.FileName, assembly.Hash));

            if (await file.Exists(_context.Log, _context.Token) == false)
            {
                // Write assembly
                await file.Write(assembly.Stream, _context.Log, _context.Token);
            }
        }

        // Copy the symbols nupkg to the symbols folder and create a catalog details page for it.
        private async Task AddSymbolsNupkgToFeed(PackageInput package)
        {
            // Write .nupkg to feed
            var packagePath = SymbolsIndexUtility.GetSymbolsNupkgPath(package.Identity);
            var packageFile = _context.Source.Get(packagePath);

            await packageFile.Write(File.OpenRead(package.PackagePath), _context.Log, _context.Token);

            // Write catalog entry to the symbols folder for the package
            var detailsPath = SymbolsIndexUtility.GetSymbolsPackageDetailsPath(package.Identity);
            var detailsFile = _context.Source.Get(detailsPath);

            var commitId = Guid.NewGuid();

            var detailsJson = await CatalogUtility.CreatePackageDetailsWithExactUriAsync(package, detailsFile.EntityUri, packageFile.EntityUri, null, commitId, writeFileList: false);

            await detailsFile.Write(detailsJson, _context.Log, _context.Token);
        }

        private ISleetFile GetFile(string fileName, string hash)
        {
            var symbolsPath = SymbolsUtility.GetSymbolsServerPath(fileName, hash);

            return _context.Source.Get($"/symbols/{symbolsPath}");
        }

        /// <summary>
        /// file/hash/index -> package
        /// </summary>
        private Task AddAssemblyToPackageIndexAsync(PackageInput package, ISleetFile indexFile, bool isSymbolsPackage)
        {
            var index = new PackageIndexFile(_context, indexFile, persistWhenEmpty: false);

            if (isSymbolsPackage)
            {
                return index.AddSymbolsPackageAsync(package);
            }
            else
            {
                return index.AddPackageAsync(package);
            }
        }

        /// <summary>
        /// Assembly asset -> Package index
        /// </summary>
        public PackageIndexFile GetAssemblyToPackageIndex(Uri packageIndexUri)
        {
            var file = _context.Source.Get(packageIndexUri);
            return new PackageIndexFile(_context, file, persistWhenEmpty: false);
        }

        private Task AddPackageToAssemblyIndexAsync(PackageIdentity package, List<PackageFile> assemblies, bool isSymbolsPackage)
        {
            var index = GetPackageToAssetIndex(package);

            var assets = assemblies.Select(e => new AssetIndexEntry(e.AssetFile.EntityUri, e.IndexFile.EntityUri));

            if (isSymbolsPackage)
            {
                return index.AddSymbolsAssetsAsync(assets);
            }
            else
            {
                return index.AddAssetsAsync(assets);
            }
        }

        private async Task<List<PackageFile>> GetAssembliesAsync(PackageInput packageInput)
        {
            var result = new List<PackageFile>();
            var seen = new HashSet<ISleetFile>();

            using (var zip = packageInput.CreateZip())
            {
                var assemblyFiles = zip.Entries
                    .Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var pdbFiles = zip.Entries
                    .Where(e => e.FullName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var assembly in assemblyFiles)
                {
                    string assemblyHash = null;
                    string pdbHash = null;
                    ZipArchiveEntry pdbEntry = null;
                    var valid = false;
                    MemoryStream assemblyStream = null;

                    try
                    {
                        assemblyStream = await assembly.Open().AsMemoryStreamAsync();

                        using (var reader = new PEReader(assemblyStream, PEStreamOptions.LeaveOpen))
                        {
                            assemblyHash = SymbolsUtility.GetSymbolHashFromAssembly(reader);
                            pdbHash = SymbolsUtility.GetPDBHashFromAssembly(reader);
                        }

                        assemblyStream.Position = 0;
                        var assemblyWithoutExt = SleetLib.PathUtility.GetFullPathWithoutExtension(assembly.FullName);

                        pdbEntry = pdbFiles.FirstOrDefault(e =>
                            StringComparer.OrdinalIgnoreCase.Equals(
                                SleetLib.PathUtility.GetFullPathWithoutExtension(e.FullName), assemblyWithoutExt));

                        valid = true;
                    }
                    catch
                    {
                        // Ignore bad assemblies
                        var message = LogMessage.Create(LogLevel.Warning, $"Unable add symbols for {assembly.FullName}, this file will not be present in the symbol server.");
                        await _context.Log.LogAsync(message);
                    }

                    if (valid)
                    {
                        // Add .dll
                        var fileInfo = new FileInfo(assembly.FullName);
                        var dllFile = _context.Source.Get(SymbolsIndexUtility.GetAssemblyFilePath(fileInfo.Name, assemblyHash));
                        var indexFile = _context.Source.Get(SymbolsIndexUtility.GetAssemblyToPackageIndexPath(fileInfo.Name, assemblyHash));

                        // Avoid duplicates
                        if (seen.Add(dllFile))
                        {
                            result.Add(new PackageFile(fileInfo.Name, assemblyHash, assemblyStream, dllFile, indexFile));
                        }

                        // Add .pdb
                        if (pdbEntry != null)
                        {
                            var pdbFileInfo = new FileInfo(pdbEntry.FullName);
                            var pdbFile = _context.Source.Get(SymbolsIndexUtility.GetAssemblyFilePath(pdbFileInfo.Name, pdbHash));
                            var pdbIndexFile = _context.Source.Get(SymbolsIndexUtility.GetAssemblyToPackageIndexPath(pdbFileInfo.Name, pdbHash));

                            // Avoid duplicates
                            if (seen.Add(pdbFile))
                            {
                                var pdbStream = await pdbEntry.Open().AsMemoryStreamAsync();
                                result.Add(new PackageFile(pdbFileInfo.Name, pdbHash, pdbStream, pdbFile, pdbIndexFile));
                            }
                        }
                    }
                }
            }

            return result;
        }

        public async Task<IReadOnlyList<ILogMessage>> ValidateAsync()
        {
            var messages = new List<ILogMessage>();
            var expectedFiles = new HashSet<ISleetFile>
            {
                PackageIndex.File
            };

            var packages = await GetPackagesAsync();
            var symbolsPackages = await GetSymbolsPackagesAsync();

            // Verify no additional packages exist in the index
            messages.AddRange(await ValidateWithFeedIndexAsync(packages, symbolsPackages));

            // De-dupe index files between packages to avoid threading conflicts
            // 1. Find all assemblies
            var assetIndexFiles = new Dictionary<PackageIdentity, AssetIndexFile>();

            foreach (var package in packages.Concat(symbolsPackages))
            {
                if (!assetIndexFiles.ContainsKey(package))
                {
                    assetIndexFiles.Add(package, GetPackageToAssetIndex(package));
                }
            }

            // Retrieve all indexes in parallel
            await Task.WhenAll(assetIndexFiles.Values.Select(e => e.File.FetchAsync(_context.Log, _context.Token)));
            expectedFiles.UnionWith(assetIndexFiles.Select(e => e.Value.File));

            // 2. Build a mapping for every assembly of the parents (symbols and non-symbols).
            var assemblyIndexFiles = new Dictionary<AssetIndexEntry, PackageIndexFile>();
            var packageAssemblyFiles = new Dictionary<PackageIdentity, ISet<AssetIndexEntry>>();
            var packageAssemblyFilesRev = new Dictionary<AssetIndexEntry, ISet<PackageIdentity>>();
            var symbolsAssemblyFiles = new Dictionary<PackageIdentity, ISet<AssetIndexEntry>>();
            var symbolsAssemblyFilesRev = new Dictionary<AssetIndexEntry, ISet<PackageIdentity>>();

            foreach (var package in packages)
            {
                var assetIndex = assetIndexFiles[package];
                var assets = await assetIndex.GetAssetsAsync();
                packageAssemblyFiles.Add(package, assets);

                foreach (var asset in assets)
                {
                    if (!assemblyIndexFiles.ContainsKey(asset))
                    {
                        var packageIndex = GetPackageIndexFile(asset);
                        assemblyIndexFiles.Add(asset, packageIndex);
                    }

                    if (!packageAssemblyFilesRev.TryGetValue(asset, out var packageSet))
                    {
                        packageSet = new HashSet<PackageIdentity>();
                        packageAssemblyFilesRev.Add(asset, packageSet);
                    }

                    packageSet.Add(package);
                }
            }

            foreach (var package in symbolsPackages)
            {
                var assetIndex = assetIndexFiles[package];
                var assets = await assetIndex.GetSymbolsAssetsAsync();
                symbolsAssemblyFiles.Add(package, assets);

                foreach (var asset in assets)
                {
                    if (!assemblyIndexFiles.ContainsKey(asset))
                    {
                        var packageIndex = GetPackageIndexFile(asset);
                        assemblyIndexFiles.Add(asset, packageIndex);
                    }

                    if (!symbolsAssemblyFilesRev.TryGetValue(asset, out var packageSet))
                    {
                        packageSet = new HashSet<PackageIdentity>();
                        symbolsAssemblyFilesRev.Add(asset, packageSet);
                    }

                    packageSet.Add(package);
                }
            }

            // Retrieve all indexes in parallel
            await Task.WhenAll(assemblyIndexFiles.Values.Select(e => e.File.FetchAsync(_context.Log, _context.Token)));

            // Get all referenced files
            expectedFiles.UnionWith(assemblyIndexFiles
                .SelectMany(e => new[] { e.Key.Asset, e.Key.PackageIndex })
                .Select(e => _context.Source.Get(e)));

            // 3. Verify that the assembly -> package index contains the same identities.
            foreach (var asset in assemblyIndexFiles.Keys)
            {
                if (!packageAssemblyFilesRev.TryGetValue(asset, out var toAssembly))
                {
                    toAssembly = new HashSet<PackageIdentity>();
                }

                var fromAssembly = await assemblyIndexFiles[asset].GetPackagesAsync();

                var diff = new PackageDiff(toAssembly, fromAssembly);

                if (diff.HasErrors)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Checking package indexes for {asset.Asset.AbsoluteUri}");
                    sb.Append(diff.ToString());

                    messages.Add(new LogMessage(LogLevel.Error, sb.ToString()));
                }
                else
                {
                    messages.Add(new LogMessage(LogLevel.Verbose, $"Package indexes for {asset.Asset.AbsoluteUri} are valid."));
                }
            }

            foreach (var asset in assemblyIndexFiles.Keys)
            {
                if (!symbolsAssemblyFilesRev.TryGetValue(asset, out var toAssembly))
                {
                    toAssembly = new HashSet<PackageIdentity>();
                }

                var fromAssembly = await assemblyIndexFiles[asset].GetSymbolsPackagesAsync();

                var diff = new PackageDiff(toAssembly, fromAssembly);

                if (diff.HasErrors)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Checking symbols package indexes for {asset.Asset.AbsoluteUri}");
                    sb.Append(diff.ToString());

                    messages.Add(new LogMessage(LogLevel.Error, sb.ToString()));
                }
                else
                {
                    messages.Add(new LogMessage(LogLevel.Verbose, $"Symbols package indexes for {asset.Asset.AbsoluteUri} are valid."));
                }
            }

            // Check that all expected files exist
            var existsTasks = expectedFiles
                .OrderBy(e => e.EntityUri.AbsoluteUri, StringComparer.Ordinal)
                .Select(e => new KeyValuePair<ISleetFile, Task<bool>>(e, e.Exists(_context.Log, _context.Token)))
                .ToList();

            foreach (var existsTask in existsTasks)
            {
                if (await existsTask.Value)
                {
                    messages.Add(new LogMessage(LogLevel.Verbose, $"Found {existsTask.Key.EntityUri.AbsoluteUri}"));
                }
                else
                {
                    messages.Add(new LogMessage(LogLevel.Error, $"Unable to find {existsTask.Key.EntityUri.AbsoluteUri}"));
                }
            }

            return messages;
        }

        private async Task<List<ILogMessage>> ValidateWithFeedIndexAsync(ISet<PackageIdentity> packages, ISet<PackageIdentity> symbolsPackages)
        {
            var messages = new List<ILogMessage>();

            var feedIndex = new PackageIndex(_context);
            if (await feedIndex.File.ExistsWithFetch(_context.Log, _context.Token))
            {
                var feedPackages = await feedIndex.GetPackagesAsync();
                var feedSymbolsPackages = await feedIndex.GetSymbolsPackagesAsync();

                var extraPackages = packages.Except(feedPackages);
                var extraSymbolsPackages = symbolsPackages.Except(feedSymbolsPackages);

                var feedDiff = new PackageDiff(Enumerable.Empty<PackageIdentity>(), extraPackages);

                if (feedDiff.HasErrors)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Checking packages in {PackageIndex.File.EntityUri.AbsoluteUri}");
                    sb.Append(feedDiff.ToString());
                    messages.Add(new LogMessage(LogLevel.Error, sb.ToString()));
                }

                var feedSymbolsDiff = new PackageDiff(Enumerable.Empty<PackageIdentity>(), extraSymbolsPackages);

                if (feedSymbolsDiff.HasErrors)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Checking symbols packages in {PackageIndex.File.EntityUri.AbsoluteUri}");
                    sb.Append(feedSymbolsDiff.ToString());
                    messages.Add(new LogMessage(LogLevel.Error, sb.ToString()));
                }
            }

            return messages;
        }

        /// <summary>
        /// Symbols symbols nupkg file.
        /// </summary>
        public ISleetFile GetSymbolsNupkgFile(PackageIdentity package)
        {
            var path = SymbolsIndexUtility.GetSymbolsNupkgPath(package);
            return _context.Source.Get(path);
        }

        public AssetIndexFile GetPackageToAssetIndex(PackageIdentity package)
        {
            var path = SymbolsIndexUtility.GetPackageToAssemblyIndexPath(package);
            return new AssetIndexFile(_context, path, package);
        }

        public PackageIndexFile GetPackageIndexFile(AssetIndexEntry assetEntry)
        {
            var file = _context.Source.Get(assetEntry.PackageIndex);
            return new PackageIndexFile(_context, file, persistWhenEmpty: false);
        }

        public async Task ApplyOperationsAsync(SleetOperations operations)
        {
            // Remove packages
            foreach (var package in operations.ToRemove)
            {
                if (package.IsSymbolsPackage)
                {
                    await RemoveSymbolsPackageAsync(package.Identity);
                }
                else
                {
                    await RemovePackageAsync(package.Identity);
                }
            }

            // Add
            foreach (var package in operations.ToAdd)
            {
                if (package.IsSymbolsPackage)
                {
                    await AddSymbolsPackageAsync(package);
                }
                else
                {
                    await AddPackageAsync(package);
                }
            }
        }

        public Task PreLoadAsync(SleetOperations operations)
        {
            return PackageIndex.FetchAsync();
        }

        /// <summary>
        /// dll or pdb file
        /// </summary>
        private class PackageFile : IDisposable
        {
            public string FileName { get; }

            public string Hash { get; }

            public MemoryStream Stream { get; }

            public ISleetFile AssetFile { get; }

            public ISleetFile IndexFile { get; }

            public PackageFile(string fileName, string hash, MemoryStream stream, ISleetFile assetFile, ISleetFile indexFile)
            {
                AssetFile = assetFile;
                FileName = fileName;
                Hash = hash;
                Stream = stream;
                IndexFile = indexFile;
            }

            public void Dispose()
            {
                Stream?.Dispose();
            }
        }
    }
}
