using System;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    /// <summary>
    /// A virtual catalog that is not written to the feed.
    /// </summary>
    public class VirtualCatalog : ISleetService, IRootIndex
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

        public async Task AddPackageAsync(PackageInput packageInput)
        {
            // Create package details page
            var packageDetails = await CatalogUtility.CreatePackageDetailsAsync(packageInput, CatalogBaseURI, _context.CommitId, writeFileList: false);
            packageInput.PackageDetails = packageDetails;
        }

        public Task RemovePackageAsync(PackageIdentity package)
        {
            // No actions needed
            return Task.FromResult(true);
        }

        public Task FetchAsync()
        {
            return RootIndexFile.FetchAsync(_context.Log, _context.Token);
        }
    }
}
