namespace Sleet
{
    internal class Constants
    {
        internal const string HelpOption = "-h|--help";
        internal const string ConfigOption = "-c|--config";
        internal const string ConfigDesc = "sleet.json file to read sources and settings from. Specify 'none' to avoid loading.";
        internal const string SourceOption = "-s|--source";
        internal const string SourceDesc = "Source name from sleet.json.";
        internal const string VerboseOption = "--verbose";
        internal const string VerboseDesc = "Write out additional messages for verbose output.";
        internal const string EnableCatalogOption = "--with-catalog";
        internal const string EnableCatalogDesc = "Enable the feed catalog and all change history tracking.";
        internal const string EnableSymbolsFeedOption = "--with-symbols";
        internal const string EnableSymbolsFeedDesc = "Enable symbols server.";
        internal const string PropertyOption = "-p|--property";
        internal const string PropertyDescription = "Define a key=value pair. Used for tokenized sleet.json files or env var values.";
    }
}
