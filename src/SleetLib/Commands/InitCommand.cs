using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    public static class InitCommand
    {
        public static Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, ILogger log)
        {
            var token = CancellationToken.None;
            return RunAsync(settings, source, enableCatalog: false, enableSymbols: false, log: log, token: token);
        }

        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, bool enableCatalog, bool enableSymbols, ILogger log, CancellationToken token)
        {
            var feedSettings = await FeedSettingsUtility.GetSettingsOrDefault(source, log, token);

            feedSettings.CatalogEnabled = enableCatalog;
            feedSettings.SymbolsEnabled = enableSymbols;

            return await InitAsync(settings, source, feedSettings, log, token);
        }

        public static Task<bool> InitAsync(SleetContext context)
        {
            return InitAsync(context.LocalSettings, context.Source, context.SourceSettings, context.Log, context.Token);
        }

        public static Task<bool> InitAsync(LocalSettings settings, ISleetFileSystem source, FeedSettings feedSettings, ILogger log, CancellationToken token)
        {
            return InitAsync(settings, source, feedSettings, autoCreateBucket: true, log: log, token: token);
        }

        public static async Task<bool> InitAsync(LocalSettings settings, ISleetFileSystem source, FeedSettings feedSettings, bool autoCreateBucket, ILogger log, CancellationToken token)
        {
            SourceUtility.ValidateFileSystem(source);
            await SourceUtility.EnsureBucketOrThrow(source, autoCreateBucket, log, token);

            var exitCode = true;
            var noChanges = true;
            var now = DateTimeOffset.UtcNow;

            var context = new SleetContext()
            {
                LocalSettings = settings,
                Source = source,
                SourceSettings = feedSettings,
                Log = log,
                Token = token,
                OperationStart = now
            };

            log.LogMinimal($"Initializing {source.BaseURI.AbsoluteUri}");

            // Validate source
            var exists = await source.Validate(log, token);

            if (!exists)
            {
                return false;
            }

            // Create service index.json
            noChanges &= !await CreateServiceIndexAsync(source, log, token, now);

            var serviceIndexFile = source.Get("index.json");
            var serviceIndexJson = await serviceIndexFile.GetJson(log, token);
            var serviceIndexJsonBefore = serviceIndexJson.DeepClone();

            serviceIndexJson["resources"] = new JArray();

            // Create sleet.settings.json
            noChanges &= !await CreateSettingsAsync(source, feedSettings, log, token, now, serviceIndexJson);

            // Create catalog/index.json
            if (feedSettings.CatalogEnabled)
            {
                noChanges &= !await CreateCatalogAsync(source, log, token, now, serviceIndexJson);
            }

            // Create autocomplete
            noChanges &= !await CreateAutoCompleteAsync(source, log, token, now, serviceIndexJson);

            // Create search
            noChanges &= !await CreateSearchAsync(source, log, token, now, serviceIndexJson);

            // Create package index
            noChanges &= !await CreatePackageIndexAsync(context, serviceIndexJson);

            // Additional entries
            AddServiceIndexEntry(source.BaseURI, "registration/", "RegistrationsBaseUrl/3.4.0", "Package registrations used for search and packages.config.", serviceIndexJson);
            AddServiceIndexEntry(source.BaseURI, "", "ReportAbuseUriTemplate/3.0.0", "Report abuse template.", serviceIndexJson);
            AddServiceIndexEntry(source.BaseURI, "flatcontainer/", "PackageBaseAddress/3.0.0", "Packages used by project.json", serviceIndexJson);
            AddServiceIndexEntry(source.BaseURI, "flatcontainer/{lower_id}/{lower_version}/readme", "ReadmeUriTemplate/6.13.0", "URI template used by NuGet Client to construct a URL for downloading a package's README.", serviceIndexJson);

            // Add symbols feed if enabled
            if (feedSettings.SymbolsEnabled)
            {
                await AddSymbolsFeedAsync(source, serviceIndexJson, context);
            }

            // Check if services changed
            noChanges &= serviceIndexJsonBefore.Equals(serviceIndexJson);

            if (noChanges)
            {
                throw new InvalidOperationException("Source is already initialized. No actions taken.");
            }

            // Write the service index out
            await serviceIndexFile.Write(serviceIndexJson, log, token);

            // Save all
            exitCode &= await source.Commit(log, token);

            if (exitCode)
            {
                log.LogMinimal($"Successfully initialized {source.BaseURI.AbsoluteUri}");
            }
            else
            {
                log.LogError($"Failed to initialize {source.BaseURI.AbsoluteUri}");
            }

            return exitCode;
        }

        private static async Task AddSymbolsFeedAsync(ISleetFileSystem source, JObject serviceIndexJson, SleetContext context)
        {
            AddServiceIndexEntry(source.BaseURI, "symbols/packages/index.json", "http://schema.emgarten.com/sleet#SymbolsPackageIndex/1.0.0", "Packages indexed in the symbols feed.", serviceIndexJson);
            AddServiceIndexEntry(source.BaseURI, "symbols/", "http://schema.emgarten.com/sleet#SymbolsServer/1.0.0", "Symbols server containing dll and pdb files.", serviceIndexJson);

            var symbols = new Symbols(context);
            await symbols.PackageIndex.InitAsync();
        }

        private static void AddServiceIndexEntry(Uri baseUri, string relativeFilePath, string type, string comment, JObject json)
        {
            var id = UriUtility.GetPath(baseUri, relativeFilePath);

            RemoveServiceIndexEntry(id, json);

            var array = (JArray)json["resources"];

            array.Add(GetServiceIndexEntry(baseUri, relativeFilePath, type, comment));
        }

        private static JObject GetServiceIndexEntry(Uri baseUri, string relativeFilePath, string type, string comment)
        {
            var id = UriUtility.GetPath(baseUri, relativeFilePath);
            var url = id.AbsoluteUri;

            // Remove encoding for templates
            url = url.Replace("%7Blower_id%7D", "{lower_id}");
            url = url.Replace("%7Blower_version%7D", "{lower_version}");

            var json = new JObject
            {
                ["@id"] = url,
                ["@type"] = type,
                ["comment"] = comment
            };

            return json;
        }

        private static void RemoveServiceIndexEntry(Uri id, JObject json)
        {
            var array = (JArray)json["resources"];

            foreach (var item in array.Where(e => id.Equals(((JObject)e).GetIdUri())))
            {
                array.Remove(item);
            }
        }

        private static async Task<bool> CreateFromTemplateAsync(
            ISleetFileSystem source,
            ILogger log,
            DateTimeOffset now,
            string templatePath,
            string sourcePath,
            CancellationToken token)
        {
            var remoteFile = source.Get(sourcePath);

            if (!await remoteFile.Exists(log, token))
            {
                var json = await TemplateUtility.LoadTemplate(templatePath, now, source.BaseURI);
                await remoteFile.Write(JObject.Parse(json), log, token);

                return true;
            }

            return false;
        }

        private static async Task<bool> CreateCatalogAsync(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now, JObject serviceIndexJson)
        {
            AddServiceIndexEntry(source.BaseURI, "catalog/index.json", "Catalog/3.0.0", "Catalog service.", serviceIndexJson);

            return await CreateFromTemplateAsync(source, log, now, "CatalogIndex", "catalog/index.json", token);
        }

        private static async Task<bool> CreateAutoCompleteAsync(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now, JObject serviceIndexJson)
        {
            AddServiceIndexEntry(source.BaseURI, "autocomplete/query", "SearchAutocompleteService/3.0.0-beta", "Powershell autocomplete.", serviceIndexJson);

            return await CreateFromTemplateAsync(source, log, now, "AutoComplete", "autocomplete/query", token);
        }

        private static async Task<bool> CreateSearchAsync(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now, JObject serviceIndexJson)
        {
            AddServiceIndexEntry(source.BaseURI, "search/query", "SearchQueryService/3.0.0-beta", "Static package list in search result form.", serviceIndexJson);

            return await CreateFromTemplateAsync(source, log, now, "Search", "search/query", token);
        }

        private static async Task<bool> CreateServiceIndexAsync(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            return await CreateFromTemplateAsync(source, log, now, "ServiceIndex", "index.json", token);
        }

        private static async Task<bool> CreateSettingsAsync(ISleetFileSystem source, FeedSettings feedSettings, ILogger log, CancellationToken token, DateTimeOffset now, JObject serviceIndexJson)
        {
            AddServiceIndexEntry(source.BaseURI, "sleet.settings.json", "http://schema.emgarten.com/sleet#SettingsFile/1.0.0", "Sleet feed settings.", serviceIndexJson);

            // Create new file.
            var result = await CreateFromTemplateAsync(source, log, now, "Settings", "sleet.settings.json", token);

            // Write out the current settings.
            await FeedSettingsUtility.SaveSettings(source, feedSettings, log, token);

            return result;
        }

        private static async Task<bool> CreatePackageIndexAsync(SleetContext context, JObject serviceIndexJson)
        {
            var packageIndex = context.Source.Get("sleet.packageindex.json");

            AddServiceIndexEntry(context.Source.BaseURI, "sleet.packageindex.json", "http://schema.emgarten.com/sleet#PackageIndex/1.0.0", "Sleet package index.", serviceIndexJson);

            if (!await packageIndex.Exists(context.Log, context.Token))
            {
                var index = new PackageIndex(context);
                await index.InitAsync();

                return true;
            }

            return false;
        }
    }
}