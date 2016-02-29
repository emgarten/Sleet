using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Logging;

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

            if (noChanges)
            {
                throw new InvalidOperationException("Source is already initialized. No actions taken.");
            }

            // Save all
            await source.Commit(log, token);

            return exitCode;
        }

        private static async Task<bool> CreateCatalog(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            var remoteFile = source.Get("catalog/index.json");
            var localFile = await remoteFile.GetLocal(log, token);

            if (!File.Exists(localFile.FullName))
            {
                using (var writer = new StreamWriter(localFile.OpenWrite()))
                {
                    var json = TemplateUtility.LoadTemplate("CatalogIndex", now, source.Root);

                    writer.WriteLine(json);
                }

                return true;
            }

            return false;
        }

        private static async Task<bool> CreateAutoComplete(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            var remoteFile = source.Get("autocomplete/query");
            var localFile = await remoteFile.GetLocal(log, token);

            if (!File.Exists(localFile.FullName))
            {
                using (var writer = new StreamWriter(localFile.OpenWrite()))
                {
                    var json = TemplateUtility.LoadTemplate("AutoComplete", now, source.Root);

                    writer.WriteLine(json);
                }

                return true;
            }

            return false;
        }

        private static async Task<bool> CreateSearch(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            var remoteFile = source.Get("search/query");
            var localFile = await remoteFile.GetLocal(log, token);

            if (!File.Exists(localFile.FullName))
            {
                using (var writer = new StreamWriter(localFile.OpenWrite()))
                {
                    var json = TemplateUtility.LoadTemplate("Search", now, source.Root);

                    writer.WriteLine(json);
                }

                return true;
            }

            return false;
        }

        private static async Task<bool> CreateServiceIndex(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            var index = source.Get("index.json");
            var indexFile = await index.GetLocal(log, token);

            if (!File.Exists(indexFile.FullName))
            {
                using (var writer = new StreamWriter(indexFile.OpenWrite()))
                {
                    var json = TemplateUtility.LoadTemplate("ServiceIndex", now, source.Root);

                    writer.WriteLine(json);
                }

                return true;
            }

            return false;
        }

        private static async Task<bool> CreateSettings(ISleetFileSystem source, ILogger log, CancellationToken token, DateTimeOffset now)
        {
            var sleetSettings = source.Get("sleet.settings.json");
            var sleetSettingsFile = await sleetSettings.GetLocal(log, token);

            if (!File.Exists(sleetSettingsFile.FullName))
            {
                using (var stream = File.OpenWrite(sleetSettingsFile.FullName))
                using (var writer = new StreamWriter(stream))
                {
                    var graph = new BasicGraph();

                    // Root node
                    graph.Assert(new Triple(sleetSettings.Path, Constants.TypeUri, Constants.GetSleetType("settings")));

                    // Properties
                    graph.Assert(new Triple(sleetSettings.Path, Constants.GetSleetType("created"), now));
                    graph.Assert(new Triple(sleetSettings.Path, Constants.GetSleetType("lastEdited"), now));

                    var json = GraphUtility.CreateJson(
                        graph,
                        GraphUtility.GetContext("Sleet"),
                        Constants.GetSleetType("settings"));

                    writer.WriteLine(json.ToString());
                }

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
