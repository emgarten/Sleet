using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    internal static class Util
    {
        internal static ISleetFileSystem CreateFileSystemOrThrow(LocalSettings settings, string sourceName, LocalCache cache)
        {
            var sourceNamePassed = !string.IsNullOrEmpty(sourceName);

            // Default to the only possible feed if one was not provided.
            if (string.IsNullOrEmpty(sourceName))
            {
                var names = GetSourceNames(settings.Json);

                if (names.Count == 1)
                {
                    sourceName = names[0];
                }
            }

            var fileSystem = FileSystemFactory.CreateFileSystem(settings, cache, sourceName);

            if (fileSystem == null)
            {
                var message = "Unable to find source. Verify that the --source parameter is correct and that sleet.json contains the named source.";

                if (!sourceNamePassed)
                {
                    var names = GetSourceNames(settings.Json);

                    if (names.Count < 1)
                    {
                        message = "The local settings file is missing or does not contain any sources. Use 'CreateConfig' to add a source.";
                    }
                    else
                    {
                        message = "The local settings file contains multiple sources. Use --source to specify the feed to use.";
                    }
                }

                throw new InvalidOperationException(message);
            }

            return fileSystem;
        }

        internal static List<string> GetSourceNames(JObject json)
        {
            var results = new List<string>();

            if (json != null)
            {
                var sources = json["sources"] as JArray;

                if (sources != null)
                {
                    foreach (var sourceEntry in sources)
                    {
                        var name = sourceEntry["name"]?.ToObject<string>();

                        if (!string.IsNullOrEmpty(name))
                        {
                            results.Add(name);
                        }
                    }
                }
            }

            return results;
        }

        internal static void ValidateRequiredOptions(IEnumerable<CommandOption> required)
        {
            // Validate parameters
            foreach (var requiredOption in required)
            {
                if (!requiredOption.HasValue())
                {
                    throw new ArgumentException($"Missing required parameter --{requiredOption.LongName}.");
                }
            }
        }

        internal static void SetVerbosity(ILogger log, bool verbose)
        {
            if (log is ConsoleLogger consoleLogger)
            {
                if (verbose)
                {
                    consoleLogger.VerbosityLevel = LogLevel.Verbose;
                }
                else
                {
                    consoleLogger.VerbosityLevel = LogLevel.Information;
                    consoleLogger.CollapseMessages = true;
                }
            }
        }
    }
}
