using System.Threading.Tasks;

namespace Sleet
{
    /// <summary>
    /// sleet.packageindex.json is a simple json index of all ids and versions contained in the feed.
    /// </summary>
    public class PackageIndex : PackageIndexFile, ISleetService, IPackagesLookup
    {
        public PackageIndex(SleetContext context)
            : base(context, "/sleet.packageindex.json", persistWhenEmpty: true)
        {
        }

        public string Name => nameof(PackageIndex);

        public override Task ApplyOperationsAsync(SleetOperations operations)
        {
            // Write the entire new set of packages directly.
            return CreateAsync(operations.UpdatedIndex);
        }
    }
}