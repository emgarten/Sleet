namespace Sleet
{
    /// <summary>
    /// sleet.packageindex.json is a simple json index of all ids and versions contained in the feed.
    /// </summary>
    public class PackageIndex : PackageIndexFile, ISleetService, IPackagesLookup
    {
        public override string Name { get; } = nameof(PackageIndex);

        public PackageIndex(SleetContext context)
            : base(context, "/sleet.packageindex.json")
        {
        }
    }
}