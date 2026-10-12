using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Helpers;
using NuGet.Versioning;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    // Set SLEET_UPDATE_SNAPSHOTS=true (or 1) to rewrite the normalized snapshots in the source tree.
    public class FeedSnapshotTests
    {
        private static readonly Regex GuidRegex = new Regex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);
        private static readonly Regex DateRegex = new Regex("^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(?:\\.\\d+)?Z$", RegexOptions.Compiled);

        [Fact]
        public async Task FeedSnapshot_GeneratedFeedMatchesGoldenFiles()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root), new Uri("https://example.org/feed/"));
                var settings = new LocalSettings();
                var context = new SleetContext()
                {
                    Token = TestContext.Current.CancellationToken,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                    {
                        CatalogEnabled = true,
                        SymbolsEnabled = true,
                        BadgesEnabled = true
                    }
                };

                var packages = CreateSnapshotPackages(packagesFolder.Root);

                await InitCommand.InitAsync(context);
                var pushResult = await PushCommand.RunAsync(context.LocalSettings, context.Source, packages, false, false, context.Log);
                pushResult.Should().BeTrue(log.GetMessages(NuGet.Common.LogLevel.Error));

                var validateResult = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);
                validateResult.Should().BeTrue(log.GetMessages(NuGet.Common.LogLevel.Error));

                var snapshot = CreateSnapshot(target.Root);
                var sourceSnapshotRoot = Path.Combine(GetSourceDirectory(), "Snapshots", "FeedSnapshot");

                if (ShouldUpdateSnapshots())
                {
                    WriteSnapshot(sourceSnapshotRoot, snapshot);
                    return;
                }

                var expectedSnapshotRoot = Path.Combine(AppContext.BaseDirectory, "Snapshots", "FeedSnapshot");
                AssertSnapshot(expectedSnapshotRoot, snapshot);
            }
        }

        private static List<string> CreateSnapshotPackages(string packagesRoot)
        {
            var alpha100 = new TestNupkg("alpha", "1.0.0");
            alpha100.Nuspec.Authors = "alpha authors";
            alpha100.Nuspec.Description = "Alpha stable package";
            alpha100.Nuspec.Summary = "Alpha stable summary";
            alpha100.Nuspec.Tags = "alpha stable";
            alpha100.Nuspec.ProjectUrl = "https://example.org/alpha";
            alpha100.Nuspec.Dependencies = new List<PackageDependencyGroup>()
            {
                new PackageDependencyGroup(NuGetFramework.AnyFramework, new List<PackageDependency>()
                {
                    new PackageDependency("beta", VersionRange.Parse("[2.0.0, )"))
                }),
                new PackageDependencyGroup(NuGetFramework.Parse("net8.0"), new List<PackageDependency>()
                {
                    new PackageDependency("gamma", VersionRange.Parse("[3.0.0]"))
                })
            };

            var alphaPrerelease = new TestNupkg("alpha", "1.1.0-beta.1");
            alphaPrerelease.Nuspec.Authors = "alpha authors";
            alphaPrerelease.Nuspec.Description = "Alpha prerelease package";
            alphaPrerelease.Nuspec.Tags = "alpha prerelease";

            var beta = new TestNupkg("beta", "2.0.0");
            beta.Nuspec.Authors = "beta authors";
            beta.Nuspec.Description = "Beta package";
            beta.Nuspec.Tags = "beta";

            var alphaSymbols = new TestNupkg("alpha", "1.0.0");
            alphaSymbols.Files.Clear();
            alphaSymbols.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
            alphaSymbols.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
            alphaSymbols.Nuspec.IsSymbolPackage = true;

            return new List<string>()
            {
                alpha100.Save(packagesRoot).FullName,
                alphaPrerelease.Save(packagesRoot).FullName,
                beta.Save(packagesRoot).FullName,
                alphaSymbols.Save(packagesRoot).FullName
            };
        }

        private static FeedSnapshot CreateSnapshot(string root)
        {
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => new FeedFile(root, path))
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .ToList();

            var guidMap = CreateCatalogGuidMap(files);
            var entries = new List<SnapshotEntry>();

            foreach (var file in files)
            {
                var normalizedPath = NormalizeString(file.RelativePath, guidMap);
                var content = File.ReadAllText(file.FullPath);

                if (TryParseJson(content, out var json))
                {
                    entries.Add(new SnapshotEntry(normalizedPath, SnapshotKind.Json, NormalizeJson(json, guidMap).ToString(Formatting.Indented).Replace("\r\n", "\n")));
                }
                else if (IsTextSnapshot(file.RelativePath))
                {
                    entries.Add(new SnapshotEntry(normalizedPath, SnapshotKind.Text, NormalizeLineEndings(content)));
                }
                else
                {
                    entries.Add(new SnapshotEntry(normalizedPath, SnapshotKind.Binary, string.Empty));
                }
            }

            return new FeedSnapshot(entries.OrderBy(entry => entry.Path, StringComparer.Ordinal).ThenBy(entry => entry.Kind).ToList());
        }

        private static string GetSourceDirectory([CallerFilePath] string sourceFilePath = "")
        {
            return Path.GetDirectoryName(sourceFilePath);
        }

        private static Dictionary<string, string> CreateCatalogGuidMap(IEnumerable<FeedFile> files)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                var match = Regex.Match(file.RelativePath, @"^catalog/data/([0-9a-fA-F-]{36})\.json$");
                if (!match.Success)
                {
                    continue;
                }

                var json = JObject.Parse(File.ReadAllText(file.FullPath));
                var id = (json["id"] ?? json["nuget:id"])?.ToString() ?? "package";
                var version = (json["version"] ?? json["nuget:version"])?.ToString() ?? "version";
                var token = Regex.Replace($"catalog-{id}-{version}", "[^A-Za-z0-9._-]", "_").ToLowerInvariant();
                result[match.Groups[1].Value] = $"__{token}__";
            }

            return result;
        }

        private static JToken NormalizeJson(JToken token, Dictionary<string, string> guidMap, string propertyName = null)
        {
            if (token is JObject obj)
            {
                var normalized = new JObject();
                foreach (var property in obj.Properties())
                {
                    normalized.Add(property.Name, NormalizeJson(property.Value, guidMap, property.Name));
                }

                return normalized;
            }

            if (token is JArray array)
            {
                var items = array.Select(item => NormalizeJson(item, guidMap, propertyName)).ToList();
                if (string.Equals(propertyName, "items", StringComparison.OrdinalIgnoreCase) && items.All(item => item is JObject))
                {
                    items = items.OrderBy(GetStableArrayKey, StringComparer.Ordinal).ToList();
                }

                return new JArray(items);
            }

            if (token is JValue value)
            {
                if (value.Type == JTokenType.Date)
                {
                    return new JValue("<timestamp>");
                }

                if (value.Type == JTokenType.String)
                {
                    var text = value.ToString();
                    if (IsTimestampProperty(propertyName) || DateRegex.IsMatch(text))
                    {
                        return new JValue("<timestamp>");
                    }

                    if (string.Equals(propertyName, "commitId", StringComparison.OrdinalIgnoreCase))
                    {
                        return new JValue("<commitId>");
                    }

                    if (string.Equals(propertyName, "packageHash", StringComparison.OrdinalIgnoreCase))
                    {
                        return new JValue("<packageHash>");
                    }

                    // The sleet assembly version changes with every build and release.
                    if (string.Equals(propertyName, "sleet:version", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(propertyName, "sleet:toolVersion", StringComparison.OrdinalIgnoreCase))
                    {
                        return new JValue("<sleetVersion>");
                    }

                    return new JValue(NormalizeString(text, guidMap));
                }

                if (value.Type == JTokenType.Integer
                    && (string.Equals(propertyName, "packageSize", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(propertyName, "length", StringComparison.OrdinalIgnoreCase)))
                {
                    // Package sizes and entry lengths include the test nuspec, which is written with the OS newline.
                    return new JValue(0);
                }
            }

            return token.DeepClone();
        }

        private static string NormalizeString(string value, Dictionary<string, string> guidMap)
        {
            var normalized = value.Replace('\\', '/');
            foreach (var pair in guidMap)
            {
                normalized = normalized.Replace(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase);
            }

            return GuidRegex.Replace(normalized, "<guid>");
        }

        private static string GetStableArrayKey(JToken token)
        {
            var obj = (JObject)token;
            return string.Join("|", new[]
            {
                obj["nuget:id"]?.ToString() ?? obj["id"]?.ToString() ?? string.Empty,
                obj["nuget:version"]?.ToString() ?? obj["version"]?.ToString() ?? string.Empty,
                obj["@id"]?.ToString() ?? string.Empty
            });
        }

        private static bool IsTimestampProperty(string propertyName)
        {
            return string.Equals(propertyName, "commitTimeStamp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "created", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "lastEdited", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "published", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "lastUpdated", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "lastWriteTime", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "nuget:lastCreated", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "nuget:lastDeleted", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTextSnapshot(string relativePath)
        {
            return relativePath.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)
                || relativePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                || relativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseJson(string content, out JToken token)
        {
            try
            {
                token = JToken.Parse(content);
                return true;
            }
            catch (JsonReaderException)
            {
                token = null;
                return false;
            }
        }

        private static void AssertSnapshot(string expectedRoot, FeedSnapshot actual)
        {
            var expectedPaths = File.ReadAllLines(Path.Combine(expectedRoot, "paths.txt"));
            var actualPaths = actual.Entries.Select(entry => entry.ManifestLine).ToArray();
            actualPaths.Should().Equal(expectedPaths, FirstDifference("feed file set", expectedPaths, actualPaths));

            foreach (var entry in actual.Entries.Where(entry => entry.Kind != SnapshotKind.Binary))
            {
                var expectedPath = Path.Combine(expectedRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
                File.Exists(expectedPath).Should().BeTrue($"snapshot file is missing for {entry.Path}");
                var expected = NormalizeLineEndings(File.ReadAllText(expectedPath));
                if (expected.EndsWith("\n", StringComparison.Ordinal))
                {
                    expected = expected.Substring(0, expected.Length - 1);
                }

                if (!string.Equals(expected, entry.Content, StringComparison.Ordinal))
                {
                    throw new Xunit.Sdk.XunitException($"Snapshot mismatch for {entry.Path}.{Environment.NewLine}{FirstDifference(entry.Path, expected, entry.Content)}{Environment.NewLine}Actual:{Environment.NewLine}{entry.Content}");
                }
            }
        }

        private static void WriteSnapshot(string snapshotRoot, FeedSnapshot snapshot)
        {
            if (Directory.Exists(snapshotRoot))
            {
                Directory.Delete(snapshotRoot, recursive: true);
            }

            Directory.CreateDirectory(snapshotRoot);
            WriteUtf8NoBom(Path.Combine(snapshotRoot, "paths.txt"), string.Join("\n", snapshot.Entries.Select(entry => entry.ManifestLine)) + "\n");

            foreach (var entry in snapshot.Entries.Where(entry => entry.Kind != SnapshotKind.Binary))
            {
                var path = Path.Combine(snapshotRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                WriteUtf8NoBom(path, entry.Content.EndsWith("\n", StringComparison.Ordinal) ? entry.Content : entry.Content + "\n");
            }
        }

        private static void WriteUtf8NoBom(string path, string content)
        {
            File.WriteAllText(path, NormalizeLineEndings(content), new UTF8Encoding(false));
        }

        private static string NormalizeLineEndings(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string FirstDifference(string name, IReadOnlyList<string> expected, IReadOnlyList<string> actual)
        {
            var max = Math.Max(expected.Count, actual.Count);
            for (var i = 0; i < max; i++)
            {
                var expectedLine = i < expected.Count ? expected[i] : "<missing>";
                var actualLine = i < actual.Count ? actual[i] : "<missing>";
                if (!string.Equals(expectedLine, actualLine, StringComparison.Ordinal))
                {
                    return $"First difference in {name} at line {i + 1}: expected '{expectedLine}', actual '{actualLine}'.";
                }
            }

            return $"No difference found in {name}.";
        }

        private static string FirstDifference(string name, string expected, string actual)
        {
            var expectedLines = NormalizeLineEndings(expected).Split('\n');
            var actualLines = NormalizeLineEndings(actual).Split('\n');
            return FirstDifference(name, expectedLines, actualLines);
        }

        private static bool ShouldUpdateSnapshots()
        {
            var value = Environment.GetEnvironmentVariable("SLEET_UPDATE_SNAPSHOTS");
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class FeedFile
        {
            public FeedFile(string root, string fullPath)
            {
                FullPath = fullPath;
                RelativePath = Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
            }

            public string FullPath { get; }

            public string RelativePath { get; }
        }

        private sealed class FeedSnapshot
        {
            public FeedSnapshot(IReadOnlyList<SnapshotEntry> entries)
            {
                Entries = entries;
            }

            public IReadOnlyList<SnapshotEntry> Entries { get; }
        }

        private sealed class SnapshotEntry
        {
            public SnapshotEntry(string path, SnapshotKind kind, string content)
            {
                Path = path;
                Kind = kind;
                Content = content;
            }

            public string Path { get; }

            public SnapshotKind Kind { get; }

            public string Content { get; }

            public string ManifestLine => $"{Kind.ToString().ToLowerInvariant()}\t{Path}";
        }

        private enum SnapshotKind
        {
            Json,
            Text,
            Binary
        }
    }
}
