using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Versioning;

namespace Sleet
{
    public static class UpgradeUtility
    {
        public static async Task<SemanticVersion> GetSleetVersionAsync(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            var indexPath = fileSystem.Get("index.json");
            var json = await indexPath.GetJson(log, token);
            var sleetVersion = json.GetValue("sleet:version")?.ToString();
            if (!SemanticVersion.TryParse(sleetVersion, out var version))
            {
                throw new InvalidOperationException("Invalid sleet:version in index.json");
            }

            return version;
        }

        public static async Task<bool> UpgradeIfNeededAsync(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            var sourceVersion = await GetSleetVersionAsync(fileSystem, log, token);

            var assemblyVersion = AssemblyVersionHelper.GetVersion();

            if (sourceVersion < assemblyVersion)
            {
                // upgrade
                log.LogInformation($"Upgrading source from {sourceVersion} to {assemblyVersion}.");

                var indexPath = fileSystem.Get("index.json");
                var json = await indexPath.GetJson(log, token);
                json["sleet:version"] = assemblyVersion.ToFullVersionString();

                await indexPath.Write(json, log, token);
            }
            else if (sourceVersion > assemblyVersion)
            {
                throw new InvalidOperationException($"{fileSystem.BaseURI} was created using a newer version of this tool: {sourceVersion}. Use the same or higher version to make changes.");
            }

            return false;
        }
    }
}