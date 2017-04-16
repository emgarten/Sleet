namespace Sleet
{
    public class FeedSettings
    {
        /// <summary>
        /// Entries per catalog page.
        /// </summary>
        public int CatalogPageSize { get; set; } = 1024;

        /// <summary>
        /// If false the catalog will not be written to the feed.
        /// </summary>
        public bool CatalogEnabled { get; set; } = true;

        /// <summary>
        /// If false the symbol feed will be disabled.
        /// </summary>
        public bool SymbolsEnabled { get; set; } = true;
    }
}