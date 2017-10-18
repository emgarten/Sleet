using System;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;

namespace Sleet
{
    public static class FileSystemFactory
    {
        /// <summary>
        /// Parses sleet.json to find the source and constructs it.
        /// </summary>
        public static ISleetFileSystem CreateFileSystem(LocalSettings settings, LocalCache cache, string source)
        {
            ISleetFileSystem result = null;

            var sources = settings.Json["sources"] as JArray;

            if (sources == null)
            {
                throw new ArgumentException("Invalid config. No sources found.");
            }

            if (sources != null)
            {
                foreach (var sourceEntry in sources)
                {
                    if (source.Equals(sourceEntry["name"]?.ToObject<string>(), StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(sourceEntry["path"]?.ToString()))
                        {
                            throw new ArgumentException("Missing path for account.");
                        }

                        var path = sourceEntry["path"]?.ToObject<string>();
                        var baseURI = sourceEntry["baseURI"]?.ToObject<string>() ?? path;
                        var relativePath = sourceEntry["relativePath"]?.ToObject<string>();
                        var type = sourceEntry["type"]?.ToObject<string>().ToLowerInvariant();

                        if (type == "local")
                        {
                            result = new PhysicalFileSystem(cache, UriUtility.CreateUri(path), UriUtility.CreateUri(baseURI));
                        }
                        else if (type == "azure")
                        {
                            var connectionString = sourceEntry["connectionString"]?.ToObject<string>();
                            var container = sourceEntry["container"]?.ToObject<string>();

                            if (string.IsNullOrEmpty(connectionString))
                            {
                                throw new ArgumentException("Missing connectionString for azure account.");
                            }

                            if (string.IsNullOrEmpty(container))
                            {
                                throw new ArgumentException("Missing container for azure account.");
                            }

                            var azureAccount = CloudStorageAccount.Parse(connectionString);

                            result = new AzureFileSystem(cache, UriUtility.CreateUri(path), UriUtility.CreateUri(baseURI), azureAccount, container, relativePath);
                        }
                    }
                }
            }

            return result;
        }
    }
}