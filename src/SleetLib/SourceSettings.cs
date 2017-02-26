namespace Sleet
{
    public class SourceSettings
    {
        /// <summary>
        /// Entries per catalog page.
        /// </summary>
        public int CatalogPageSize { get; set; } = 1024;

        /// <summary>
        /// If false the catalog will not be written to the feed.
        /// </summary>
        public bool CatalogEnabled { get; set; } = true;
    }
}