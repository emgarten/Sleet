using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    public static class InitCommand
    {
        public static Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, ILogger log)
        {
            var token = CancellationToken.None;
            return RunAsync(settings, source, disableCatalog: false, disableSymbols: false, log: log, token: token);
        }

        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, bool disableCatalog, bool disableSymbols, ILogger log, CancellationToken token)
        {
            var feedSettings = await FeedSettingsUtility.GetSettingsOrDefault(source, log, token);

            feedSettings.CatalogEnabled = !disableCatalog;
            feedSettings.SymbolsFeedEnabled = !disableSymbols;

            return await InitAsync(settings, source, feedSettings, log, token);
        }

        public static async Task<bool> InitAsync(LocalSettings settings, ISleetFileSystem source, FeedSettings feedSettings, ILogger log, CancellationToken token)
        {
            var exitCode = true;
            var noChanges = true;
            var now = DateTimeOffset.UtcNow;

            log.LogMinimal($"Initializing {source.BaseURI.AbsoluteUri}");

            // Validate source
            var exists = await source.Validate(log, token);

            if (!exists)
            {
                return false;
            }

            // Create sleet.settings.json
            noChanges &= !await CreateSettingsAsync(source, feedSettings, log, token, now);

            // Create service index.json
            noChanges &= !await CreateServiceIndexAsync(source, log, token, now);

            // Create catalog/index.json
            if (feedSettings.CatalogEnabled)
            {
                noChanges &= !await CreateCatalogAsync(source, log, token, now);
            }

            // Create autocomplete
            noChanges &= !await CreateAutoCompleteAsync(source, log, token, now);

            // Create search
            noChanges &= !await CreateSearchAsync(source, log, token, now);

            // Create package index
            noChanges &= !await CreatePackageIndexAsync(source, log, token, now);

            if (noChanges)
            {
                throw new InvalidOperationException("Source is already initialized. No actions taken.");
            }

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
                var json = TemplateUtility.LoadTemplate(templatePath, now, source.BaseURI);
                await remoteFile.Write(JObject.Parse(json), log, token);

                return true;
            }

            return false;
        }

        private static async Task<bool> CreateCatalogAsync(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            return await CreateFromTemplateAsync(source, log, now, "CatalogIndex", "catalog/index.json", token);
        }

        private static async Task<bool> CreateAutoCompleteAsync(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            return await CreateFromTemplateAsync(source, log, now, "AutoComplete", "autocomplete/query", token);
        }

        private static async Task<bool> CreateSearchAsync(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            return await CreateFromTemplateAsync(source, log, now, "Search", "search/query", token);
        }

        private static async Task<bool> CreateServiceIndexAsync(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            return await CreateFromTemplateAsync(source, log, now, "ServiceIndex", "index.json", token);
        }

        private static async Task<bool> CreateSettingsAsync(ISleetFileSystem source, FeedSettings feedSettings, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            // Create new file.
            var result = await CreateFromTemplateAsync(source, log, now, "Settings", "sleet.settings.json", token);

            // Write out the current settings.
            await FeedSettingsUtility.SaveSettings(source, feedSettings, log, token);

            return result;
        }

        private static async Task<bool> CreatePackageIndexAsync(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            var packageIndex = source.Get("/sleet.packageindex.json");

            if (!await packageIndex.Exists(log, token))
            {
                var json = new JObject
                {
                    { "created", new JValue(now.GetDateString()) },
                    { "lastEdited", new JValue(now.GetDateString()) },

                    { "packages", new JObject() }
                };
                await packageIndex.Write(json, log, token);

                return true;
            }

            return false;
        }
    }
}