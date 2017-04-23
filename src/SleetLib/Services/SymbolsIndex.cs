namespace Sleet
{
    /// <summary>
    /// An index of all packages that have been indexed in symbols/
    /// </summary>
    public class SymbolsIndex : PackageIndexFile, ISleetService, IPackagesLookup
    {
        public override string Name { get; } = nameof(SymbolsIndex);

        public SymbolsIndex(SleetContext context)
            : base(context, "/symbolspackages/symbolsindex.json")
        {
        }
    }
}