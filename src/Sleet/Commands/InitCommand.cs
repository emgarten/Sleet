using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    internal static class InitCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("init", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Initialize a new sleet feed.";

            var optionConfigFile = cmd.Option("-c|--config", "sleet.json file to read sources and settings from.",
                CommandOptionType.SingleValue);

            var sourceName = cmd.Option("-s|--source", "Source from sleet.json.",
                CommandOptionType.SingleValue);

            cmd.HelpOption("-?|-h|--help");

            var required = new List<CommandOption>()
            {
                sourceName
            };

            cmd.OnExecute(async () =>
            {
                cmd.ShowRootCommandFullNameAndVersion();

                // Validate parameters
                foreach (var requiredOption in required)
                {
                    if (!requiredOption.HasValue())
                    {
                        throw new ArgumentException($"Missing required parameter --{requiredOption.LongName}.");
                    }
                }

                var settings = LocalSettings.Load(optionConfigFile.Value());

                using (var cache = new LocalCache())
                {
                    var fileSystem = FileSystemFactory.CreateFileSystem(settings, cache, sourceName.Value());

                    if (fileSystem == null)
                    {
                        throw new InvalidOperationException("Unable to find source. Verify that the --source parameter is correct and that sleet.json contains the named source.");
                    }

                    return await RunCore(settings, fileSystem, log);
                }
            });
        }

        public static async Task<int> RunCore(LocalSettings settings, ISleetFileSystem source, ILogger log)
        {
            var exitCode = 0;

            var noChanges = true;
            var token = CancellationToken.None;
            var now = DateTimeOffset.UtcNow;

            // Validate source
            var exists = await source.Validate(log, token);

            if (!exists)
            {
                return 1;
            }

            // Create sleet.settings.json
            noChanges &= !await CreateSettings(source, log, token, now);

            // Create service index.json
            noChanges &= !await CreateServiceIndex(source, log, token, now);

            // Create catalog/index.json
            noChanges &= !await CreateCatalog(source, log, token, now);

            // Create autocomplete
            noChanges &= !await CreateAutoComplete(source, log, token, now);

            // Create search
            noChanges &= !await CreateSearch(source, log, token, now);

            // Create package index
            noChanges &= !await CreatePackageIndex(source, log, token, now);

            if (noChanges)
            {
                throw new InvalidOperationException("Source is already initialized. No actions taken.");
            }

            // Save all
            await source.Commit(log, token);

            return exitCode;
        }

        private static async Task<bool> CreateFromTemplate(
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

        private static async Task<bool> CreateCatalog(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            return await CreateFromTemplate(source, log, now, "CatalogIndex", "catalog/index.json", token);
        }

        private static async Task<bool> CreateAutoComplete(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            return await CreateFromTemplate(source, log, now, "AutoComplete", "autocomplete/query", token);
        }

        private static async Task<bool> CreateSearch(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            return await CreateFromTemplate(source, log, now, "Search", "search/query", token);
        }

        private static async Task<bool> CreateServiceIndex(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            return await CreateFromTemplate(source, log, now, "ServiceIndex", "index.json", token);
        }

        private static async Task<bool> CreateSettings(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            var sleetSettings = source.Get("sleet.settings.json");

            if (!await sleetSettings.Exists(log, token))
            {
                var json = JsonUtility.Create(sleetSettings.EntityUri, "Settings");

                json.Add("created", new JValue(now.GetDateString()));
                json.Add("lastEdited", new JValue(now.GetDateString()));

                await sleetSettings.Write(json, log, token);

                return true;
            }

            return false;
        }

        private static async Task<bool> CreatePackageIndex(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            var packageIndex = source.Get("/sleet.packageindex.json");

            if (!await packageIndex.Exists(log, token))
            {
                var json = new JObject();

                json.Add("created", new JValue(now.GetDateString()));
                json.Add("lastEdited", new JValue(now.GetDateString()));

                json.Add("packages", new JObject());

                await packageIndex.Write(json, log, token);

                return true;
            }

            return false;
        }
    }

    public static class InitCommandTestHook
    {
        public static Task<int> RunCore(LocalSettings settings, ISleetFileSystem source, ILogger log)
        {
            return InitCommand.RunCore(settings, source, log);
        }
    }
}
