using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    /// <summary>
    /// A virtual catalog that is not written to the feed.
    /// </summary>
    public class VirtualCatalog : ISleetService, IRootIndex, IAddRemovePackages
    {
        private readonly SleetContext _context;

        public string Name { get; } = "VirtualCatalog";

        /// <summary>
        /// Example: virtualcatalog/index.json
        /// </summary>
        public string RootIndex
        {
            get
            {
                var rootURI = UriUtility.GetPath(CatalogBaseURI, "index.json");

                return UriUtility.GetRelativePath(_context.Source.BaseURI, rootURI);
            }
        }

        /// <summary>
        /// Catalog index.json file
        /// </summary>
        public ISleetFile RootIndexFile
        {
            get
            {
                return _context.Source.Get(RootIndex);
            }
        }

        /// <summary>
        /// Example: http://tempuri.org/virtualcatalog/
        /// </summary>
        public Uri CatalogBaseURI { get; }

        public VirtualCatalog(SleetContext context)
            : this(context, UriUtility.GetPath(context.Source.BaseURI, "virtualcatalog/"))
        {
        }

        public VirtualCatalog(SleetContext context, Uri catalogBaseURI)
        {
            _context = context;
            CatalogBaseURI = catalogBaseURI;
        }

        public Task AddPackageAsync(PackageInput packageInput)
        {
            return AddPackagesAsync(new[] { packageInput });
        }

        public Task RemovePackageAsync(PackageIdentity package)
        {
            // No actions needed
            return Task.FromResult(true);
        }

        public Task RemovePackagesAsync(IEnumerable<PackageIdentity> packages)
        {
            // No actions needed
            return Task.FromResult(true);
        }

        public Task FetchAsync()
        {
            return RootIndexFile.FetchAsync(_context.Log, _context.Token);
        }

        public Task AddPackagesAsync(IEnumerable<PackageInput> packageInputs)
        {
            // Create package details page
            var tasks = packageInputs.Select(e => new Func<Task>(() => CreateDetailsForAdd(e)));
            return TaskUtils.RunAsync(tasks, useTaskRun: true, token: CancellationToken.None);
        }

        private async Task CreateDetailsForAdd(PackageInput packageInput)
        {
            // Create a a details page and assign it to the input
            var nupkgUri = packageInput.GetNupkgUri(_context);
            var packageDetails = await CatalogUtility.CreatePackageDetailsAsync(packageInput, CatalogBaseURI, nupkgUri, _context.CommitId, writeFileList: false);
            packageInput.PackageDetails = packageDetails;
        }

        public Task ApplyOperationsAsync(SleetOperations operations)
        {
            return OperationsUtility.ApplyAddRemoveAsync(this, operations);
        }

        public Task PreLoadAsync(SleetOperations operations)
        {
            return Task.FromResult(true);
        }
    }
}
