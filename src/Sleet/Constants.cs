using System;
using System.Collections.Generic;
using System.Text;

namespace Sleet
{
    internal class Constants
    {
        internal const string HelpOption = "-h|--help";
        internal const string ConfigOption = "-c|--config";
        internal const string ConfigDesc = "sleet.json file to read sources and settings from.";
        internal const string SourceOption = "-s|--source";
        internal const string SourceDesc = "Source name from sleet.json.";
        internal const string VerboseOption = "-v|--verbose";
        internal const string VerboseDesc = "Write out additional messages for verbose output.";
        internal const string DisableCatalogOption = "--disable-catalog";
        internal const string DisableCatalogDesc = "Disable the feed catalog and all change history tracking. This is useful for feeds that frequently replace or remove packages.";
        internal const string DisableSymbolsFeedOption = "--disable-symbols-feed";
        internal const string DisableSymbolsFeedDesc = "Disable the symbols feed. This will block the upload of symbol packages.";
    }
}
