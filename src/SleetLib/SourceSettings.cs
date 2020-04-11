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
        public bool CatalogEnabled { get; set; } = false;

        /// <summary>
        /// If false the symbol feed will be disabled.
        /// </summary>
        public bool SymbolsEnabled { get; set; } = false;

        /// <summary>
        /// Package retention - Maximum number of stable versions
        /// </summary>
        public int? RetentionMaxStableVersions { get; set; }

        /// <summary>
        /// Package retention - Maximum number of prerelease versions
        /// </summary>
        public int? RetentionMaxPrereleaseVersions { get; set; }
    }
}