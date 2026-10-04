using System.Text;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Helpers;
using NuGet.Versioning;

namespace Sleet.UX.Tests
{
    /// <summary>
    /// The packages pushed to the feed that the gallery tests browse.
    /// </summary>
    internal static class GalleryFeed
    {
        // Enough packages for three pages of the list.
        public const int FabrikamCount = 40;

        public const string CoreReadme = "# Contoso.Core\n\nCore types for Contoso.\n\n- Fast\n- Small\n";

        public const string HostileReadme = "<script>window.__sleetPwned=5</script>\n";

        // A 1x1 png.
        private static readonly byte[] IconBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

        /// <summary>
        /// Every id on the feed in the order the gallery lists them.
        /// </summary>
        public static IReadOnlyList<string> Ids { get; } = new[]
        {
            "Contoso.Core",
            "Contoso.Hostile",
            "Contoso.Meta",
            "Contoso.Preview",
            "Contoso.Util",
        }.Concat(Fabrikam(1, FabrikamCount)).ToArray();

        public static IReadOnlyList<string> Fabrikam(int first, int last)
        {
            return Enumerable.Range(first, last - first + 1).Select(i => $"Fabrikam.Package{i:00}").ToArray();
        }

        public static IEnumerable<TestNupkg> GetPackages()
        {
            var core = new TestNupkg("Contoso.Core", "1.0.0");
            core.Nuspec.Description = "Core types for Contoso.";
            yield return core;

            yield return CreateCore();

            // The prerelease label keeps its case in the registration, while
            // the flat container uses the lower case form.
            yield return new TestNupkg("Contoso.Core", "2.0.0-Beta.1");

            yield return new TestNupkg("Contoso.Util", "1.0.0");
            yield return new TestNupkg("Contoso.Preview", "0.1.0-alpha");
            yield return new TestNupkg("Contoso.Preview", "0.2.0-alpha");

            // Build metadata is kept in the package index but not in the feed layout.
            yield return new TestNupkg("Contoso.Meta", "1.0.0+build.5");

            yield return CreateHostile();

            foreach (var id in Fabrikam(1, FabrikamCount))
            {
                yield return new TestNupkg(id, "1.0.0");
            }
        }

        private static TestNupkg CreateCore()
        {
            var package = new TestNupkg("Contoso.Core", "1.1.0");
            var nuspec = package.Nuspec;
            nuspec.Title = "Contoso Core";
            nuspec.Description = "Core types for Contoso.";
            nuspec.Authors = "Contoso, Fabrikam";
            nuspec.Tags = "contoso core";
            nuspec.ProjectUrl = "https://example.com/contoso";
            nuspec.LicenseUrl = "https://example.com/license";
            nuspec.Icon = "icon.png";
            nuspec.Readme = "README.md";
            nuspec.Dependencies = new List<PackageDependencyGroup>()
            {
                new(NuGetFramework.Parse("net472"), Array.Empty<PackageDependency>()),
                new(NuGetFramework.Parse("net8.0"), new[]
                {
                    new PackageDependency("Contoso.Util", VersionRange.Parse("[1.0.0, 2.0.0)"))
                }),

                // The id of a dependency does not have to match the case of the package.
                new(NuGetFramework.Parse("netstandard2.0"), new[]
                {
                    new PackageDependency("contoso.util", VersionRange.Parse("1.0.0")),
                    new PackageDependency("Newtonsoft.Json", VersionRange.Parse("13.0.1"))
                }),
            };

            package.AddFile("icon.png", IconBytes);
            package.AddFile("README.md", Encoding.UTF8.GetBytes(CoreReadme));
            return package;
        }

        // Metadata comes from whoever published the package. None of this may
        // become markup or a script url in the gallery.
        private static TestNupkg CreateHostile()
        {
            var package = new TestNupkg("Contoso.Hostile", "1.0.0");
            var nuspec = package.Nuspec;
            nuspec.Title = "<b>Hostile</b>";
            nuspec.Description = "<img src=x onerror=\"window.__sleetPwned=1\">";
            nuspec.Authors = "<script>window.__sleetPwned=2</script>";
            nuspec.ProjectUrl = "javascript:window.__sleetPwned=3";
            nuspec.LicenseUrl = "data:text/html,<script>window.__sleetPwned=4</script>";
            nuspec.Tags = "<i>tag</i>";
            nuspec.Readme = "README.md";
            nuspec.Dependencies = new List<PackageDependencyGroup>()
            {
                new(NuGetFramework.AnyFramework, new[]
                {
                    new PackageDependency("<b>Dependency</b>", VersionRange.Parse("1.0.0"))
                }),
            };

            package.AddFile("README.md", Encoding.UTF8.GetBytes(HostileReadme));
            return package;
        }

        public static async Task CreateAsync(string root, string baseUri, string packagesFolder, IEnumerable<TestNupkg> packages)
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(packagesFolder);

            var log = new TestLogger();
            var settings = new LocalSettings();

            using (var cache = new LocalCache())
            {
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(root), UriUtility.CreateUri(baseUri));
                var success = await InitCommand.InitAsync(settings, fileSystem, new FeedSettings(), log, CancellationToken.None);

                var inputs = packages.Select(e => e.Save(packagesFolder).FullName).ToList();

                if (success && inputs.Count > 0)
                {
                    success = await PushCommand.RunAsync(settings, fileSystem, inputs, force: false, skipExisting: false, log: log);
                }

                if (!success)
                {
                    throw new InvalidOperationException("Unable to create the test feed." + Environment.NewLine + log.GetMessages());
                }
            }
        }
    }
}
