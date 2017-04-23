namespace Sleet
{
    /// <summary>
    /// An index of all .symbols.nupkg packages
    /// </summary>
    public class SymbolsPackageIndex : PackageIndexFile, ISleetService, IPackagesLookup
    {
        public override string Name { get; } = nameof(SymbolsPackageIndex);

        public SymbolsPackageIndex(SleetContext context)
            : base(context, "/symbolspackages/packageindex.json")
        {
        }
    }
}