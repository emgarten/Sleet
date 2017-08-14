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
using SleetLib;

namespace Sleet
{
    public class Symbols : ISleetService, ISymbolsAddRemovePackages, ISymbolsPackagesLookup
    {
        private readonly SleetContext _context;

        public string Name => nameof(Symbols);

        public PackageIndexFile PackageIndex { get; }

        public Symbols(SleetContext context)
        {
            _context = context;
            PackageIndex = new PackageIndexFile(context, SymbolsIndexUtility.PackageIndexPath);
        }

        public async Task AddPackageAsync(PackageInput packageInput)
        {
            // Read dll/pdb files from the package.
            var assemblies = await GetAssembliesAsync(packageInput);

            if (assemblies.Count > 0)
            {
                var tasks = new List<Task>();

                // Add the id/version to the package index.
                tasks.Add(PackageIndex.AddPackageAsync(packageInput));

                // Add dll/pdb files to the feed.
                tasks.AddRange(assemblies.Select(AddAssemblyAsync));

                // Add index of all dll/pdb files added for the package.
                tasks.Add(AddAssemblyIndexAsync(packageInput.Identity, assemblies, isSymbolsPackage: false));

                // Wait for everything to finish
                await Task.WhenAll(tasks);
            }
            else
            {
                await _context.Log.LogAsync(LogLevel.Verbose, $"No files found that could be added to the symbols feed. Skipping package {packageInput.Identity}");
            }
        }

        public Task RemovePackageAsync(PackageIdentity package)
        {
            return Task.FromResult<bool>(false);
        }

        public async Task AddSymbolsPackageAsync(PackageInput packageInput)
        {
            await PackageIndex.AddSymbolsPackageAsync(packageInput);
        }

        public Task RemoveSymbolsPackageAsync(PackageIdentity package)
        {
            throw new NotImplementedException();
        }

        public Task<ISet<PackageIdentity>> GetSymbolsPackagesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<ISet<PackageIdentity>> GetSymbolsPackagesByIdAsync(string packageId)
        {
            throw new NotImplementedException();
        }

        private async Task AddAssemblyAsync(PackageFile assembly)
        {
            var file = _context.Source.Get(SymbolsIndexUtility.GetAssemblyFilePath(assembly.FileName, assembly.Hash));

            if (await file.Exists(_context.Log, _context.Token) == false)
            {
                // Write assembly
                using (var stream = await assembly.ZipEntry.Open().AsMemoryStreamAsync())
                {
                    await file.Write(stream, _context.Log, _context.Token);
                }
            }
        }

        private ISleetFile GetFile(string fileName, string hash)
        {
            var symbolsPath = SymbolsUtility.GetSymbolsServerPath(fileName, hash);

            return _context.Source.Get($"/symbols/{symbolsPath}");
        }

        private Task AddAssemblyIndexAsync(PackageIdentity package, List<PackageFile> assemblies, bool isSymbolsPackage)
        {
            var path = SymbolsIndexUtility.GetAssemblyPackageIndexPath(package);
            var file = _context.Source.Get(path);

            // TODO: implement
            throw new NotImplementedException();

            //return Task.FromResult(0);
        }

        private async Task<List<PackageFile>> GetAssembliesAsync(PackageInput packageInput)
        {
            var result = new List<PackageFile>();

            var assemblyFiles = packageInput.Zip.Entries
                .Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var pdbFiles = packageInput.Zip.Entries
                .Where(e => e.FullName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var assembly in assemblyFiles)
            {
                string assemblyHash = null;
                string pdbHash = null;
                ZipArchiveEntry pdbEntry = null;
                var valid = false;

                try
                {
                    using (var stream = await assembly.Open().AsMemoryStreamAsync())
                    using (var reader = new PEReader(stream))
                    {
                        assemblyHash = SymbolsUtility.GetSymbolHashFromAssembly(reader);
                        pdbHash = SymbolsUtility.GetPDBHashFromAssembly(reader);
                    }

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
                    result.Add(new PackageFile()
                    {
                        FileName = fileInfo.Name,
                        Hash = assemblyHash,
                        ZipEntry = assembly
                    });

                    // Add .pdb
                    if (pdbEntry != null)
                    {
                        var pdbFileInfo = new FileInfo(pdbEntry.FullName);
                        result.Add(new PackageFile()
                        {
                            FileName = pdbFileInfo.Name,
                            Hash = pdbHash,
                            ZipEntry = pdbEntry
                        });
                    }
                }
            }

            return result;
        }

        private class PackageFile
        {
            public string FileName { get; set; }

            public string Hash { get; set; }

            public ZipArchiveEntry ZipEntry { get; set; }
        }
    }
}
