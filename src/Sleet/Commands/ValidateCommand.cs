using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Sleet
{
    internal static class ValidateCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("validate", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Validate a feed.";

            var optionConfigFile = cmd.Option("-c|--config", "sleet.json file to read sources and settings from.",
                CommandOptionType.SingleValue);

            var sourceName = cmd.Option("-s|--source", "Source name from sleet.json.",
                            CommandOptionType.SingleValue);

            cmd.HelpOption("-?|-h|--help");

            var required = new List<CommandOption>()
            {
                sourceName
            };

            cmd.OnExecute(async () =>
            {
                try
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
                }
                catch (Exception ex)
                {
                    log.LogError(ex.Message);
                    log.LogDebug(ex.ToString());
                }

                return 1;
            });
        }

        public static async Task<int> RunCore(LocalSettings settings, ISleetFileSystem source, ILogger log)
        {
            var exitCode = 0;

            var token = CancellationToken.None;

            // Check if already initialized
            await SourceUtility.VerifyInit(source, log, token);

            // Validate source
            await UpgradeUtility.UpgradeIfNeeded(source, log, token);

            // Get sleet.settings.json
            var sourceSettings = new SourceSettings();

            // Settings context used for all operations
            var context = new SleetContext()
            {
                LocalSettings = settings,
                SourceSettings = sourceSettings,
                Log = log,
                Source = source,
                Token = token
            };

            var catalog = new Catalog(context);
            var registrations = new Registrations(context);
            var flatContainer = new FlatContainer(context);
            var search = new Search(context);
            var autoComplete = new AutoComplete(context);
            var pinService = new PinService(context);
            var packageIndex = new PackageIndex(context);

            var indexedPackages = await packageIndex.GetPackageIdentities();



            return exitCode;
        }

        private class PackageDiff
        {
            /// <summary>
            /// Extra packages that should not exist.
            /// </summary>
            public HashSet<PackageIdentity> Extra { get; } = new HashSet<PackageIdentity>();

            /// <summary>
            /// Packages expected to exist that are missing.
            /// </summary>
            public HashSet<PackageIdentity> Missing { get; } = new HashSet<PackageIdentity>();

            public PackageDiff(IEnumerable<PackageIdentity> expected, IEnumerable<PackageIdentity> actual)
            {
                Extra.UnionWith(actual.Except(expected));
                Missing.UnionWith(expected.Except(actual));
            }

            public bool HasErrors
            {
                get
                {
                    return Extra.Count > 0 || Missing.Count > 0;
                }
            }

            public override string ToString()
            {
                var sb = new StringBuilder();

                sb.AppendLine($"Missing packages: {Missing.Count}");

                foreach (var package in Missing.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Version))
                {
                    sb.AppendLine($"  {package.Id} {package.Version.ToFullVersionString()}");
                }

                sb.AppendLine($"Extra packages: {Extra.Count}");

                foreach (var package in Extra.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Version))
                {
                    sb.AppendLine($"  {package.Id} {package.Version.ToFullVersionString()}");
                }

                return sb.ToString();
            }
        }
    }
}
