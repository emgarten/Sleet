using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Test.Helpers;
using Sleet;

namespace Sleet.Test.Common
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
        /// Create a package input from a nupkg, symbols packages are detected automatically.
        /// </summary>
        public static PackageInput GetPackageInput(FileInfo zipFile)
        {
            return PackageInput.Create(zipFile.FullName);
        }

        public Task Commit()
        {
            return SleetContext.Source.Commit(SleetContext.Log, SleetContext.Token);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Root.Dispose();

            foreach (var item in DisposeItems)
            {
                item.Dispose();
            }
        }
    }
}
