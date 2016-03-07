using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;
using NuGet.Versioning;

namespace Sleet
{
    public static class UpgradeUtility
    {
        public static async Task<SemanticVersion> GetSleetVersion(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            var indexPath = fileSystem.Get("index.json");
            var json = await indexPath.GetJson(log, token);
            var sleetVersion = json.GetValue("sleet:version")?.ToString();

            SemanticVersion version;
            if (!SemanticVersion.TryParse(sleetVersion, out version))
            {
                throw new InvalidOperationException("Invalid sleet:version in index.json");
            }

            return version;
        }

        public static async Task<bool> UpgradeIfNeeded(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            var sourceVersion = await GetSleetVersion(fileSystem, log, token);

            if (sourceVersion < Constants.SleetVersion)
            {
                // upgrade
                log.LogInformation($"Upgrading source from {sourceVersion} to {Constants.SleetVersion}.");

                var indexPath = fileSystem.Get("index.json");
                var json = await indexPath.GetJson(log, token);
                json["sleet:version"] = Constants.SleetVersion.ToNormalizedString();

                await indexPath.Write(json, log, token);
            }
            else if (sourceVersion > Constants.SleetVersion)
            {
                throw new InvalidOperationException($"{fileSystem.Root} was created using a newer version of this tool: {sourceVersion}. Use the same or higher version to make changes.");
            }

            return false;
        }
    }
}
