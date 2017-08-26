using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Helpers;
using NuGet.Versioning;
using Sleet;

namespace SleetLib.Tests
{
    public class SleetTestContext : IDisposable
    {
        /// <summary>
        /// Root directory
        /// </summary>
        public TestFolder Root { get; } = new TestFolder();

        /// <summary>
        /// Package inputs
        /// </summary>
        public string Packages { get; }

        /// <summary>
        /// Target feed
        /// </summary>
        public string Target { get; }

        /// <summary>
        /// Sleet Context
        /// </summary>
        public SleetContext SleetContext { get; }

        /// <summary>
        /// Additional components from the test to dispose of.
        /// </summary>
        public List<IDisposable> DisposeItems { get; } = new List<IDisposable>();

        public SleetTestContext()
        {
            Packages = Path.Combine(Root.Root, "packages");
            Target = Path.Combine(Root.Root, "target");

            SleetContext = new SleetContext()
            {
                Token = CancellationToken.None,
                LocalSettings = new LocalSettings(),
                Log = new TestLogger(),
                Source = new PhysicalFileSystem(new LocalCache(Path.Combine(Root.Root, "cache")), UriUtility.CreateUri(Target)),
                SourceSettings = new FeedSettings()
                {
                    CatalogEnabled = false,
                    SymbolsEnabled = false,
                }
            };
        }

        /// <summary>
        /// Create a package input from a zip file and register it for disposal.
        /// </summary>
        public PackageInput GetPackageInput(FileInfo zipFile)
        {
            return GetPackageInput(zipFile, isSymbols: false);
        }

        /// <summary>
        /// Create a package input from a zip file and register it for disposal.
        /// </summary>
        public PackageInput GetPackageInput(FileInfo zipFile, bool isSymbols)
        {
            var reader = new PackageArchiveReader(zipFile.FullName);

            var zip = new ZipArchive(File.OpenRead(zipFile.FullName), ZipArchiveMode.Read, false);
            var input = new PackageInput(zipFile.FullName, reader.GetIdentity(), isSymbols)
            {
                Zip = zip,
                Package = reader
            };

            DisposeItems.Add(input);
            DisposeItems.Add(reader);
            return input;
        }

        public Task Commit()
        {
            return SleetContext.Source.Commit(SleetContext.Log, SleetContext.Token);
        }

        public void Dispose()
        {
            Root.Dispose();

            foreach (var item in DisposeItems)
            {
                item.Dispose();
            }
        }
    }
}
