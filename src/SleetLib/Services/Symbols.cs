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
    public class Symbols : ISleetService
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
            await AddAssembliesAsync(packageInput);

            if (packageInput.IsSymbolsPackage)
            {
                await PackageIndex.AddSymbolsPackageAsync(packageInput);
            }
            else
            {
                await PackageIndex.AddPackageAsync(packageInput);
            }
        }

        public Task RemovePackageAsync(PackageIdentity package)
        {
            return Task.FromResult<bool>(false);
        }

        private Task AddFileIndexEntryIfNotExists()
        {
            return Task.FromResult(0);
        }

        private async Task AddFileIfNotExists(ZipArchiveEntry entry, ISleetFile file, PackageInput packageInput)
        {
            // Assembly -> Package indexes
            var packageIndex = SymbolsIndexUtility.GetPackageIndexFile(_context, packageInput.Identity);

            if (await file.Exists(_context.Log, _context.Token) == false)
            {
                // Write assembly
                using (var stream = await entry.Open().AsMemoryStreamAsync())
                {
                    await file.Write(stream, _context.Log, _context.Token);
                }

                await packageIndex.Init();
            }

            if (packageInput.IsSymbolsPackage)
            {
                await packageIndex.AddSymbolsPackageAsync(packageInput);
            }
            else
            {
                await packageIndex.AddPackageAsync(packageInput);
            }
        }

        private ISleetFile GetFile(string fileName, string hash)
        {
            var symbolsPath = SymbolsUtility.GetSymbolsServerPath(fileName, hash);

            return _context.Source.Get($"/symbols/{symbolsPath}");
        }

        private async Task AddAssembliesAsync(PackageInput packageInput)
        {
            var files = await GetAssembliesAsync(packageInput);

            foreach (var file in files)
            {
                await AddFileIfNotExists(file.Value, file.Key, packageInput);
            }
        }

        private async Task<List<KeyValuePair<ISleetFile, ZipArchiveEntry>>> GetAssembliesAsync(PackageInput packageInput)
        {
            var result = new List<KeyValuePair<ISleetFile, ZipArchiveEntry>>();

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
                    var file = GetFile(fileInfo.Name, assemblyHash);
                    result.Add(new KeyValuePair<ISleetFile, ZipArchiveEntry>(file, assembly));

                    // Add .pdb
                    if (pdbEntry != null)
                    {
                        var pdbFileInfo = new FileInfo(pdbEntry.FullName);
                        var pdbFile = GetFile(pdbFileInfo.Name, pdbHash);
                        result.Add(new KeyValuePair<ISleetFile, ZipArchiveEntry>(pdbFile, pdbEntry));
                    }
                }
            }

            return result;
        }
    }
}
