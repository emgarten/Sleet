using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
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
    // The gallery in index.html reads these files directly from the browser. These
    // tests pin the parts of the feed layout that it depends on, a change that fails
    // here needs a matching change in templateGallery.html.
    public class GalleryFeedFormatTests
    {
        private const string BaseUri = "https://example.com/feed/";

        [Fact]
        public async Task GivenPushedPackagesVerifyPackageIndexListsEveryVersionNewestFirst()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var packages = new[]
                {
                    new TestNupkg("Contoso.Core", "1.0.0"),
                    new TestNupkg("Contoso.Core", "2.0.0-Beta.1"),
                    new TestNupkg("Contoso.Core", "1.1.0"),
                    new TestNupkg("Contoso.Meta", "1.0.0+build.5"),
                };

                // Act
                await CreateFeedAsync(packagesFolder, target, cache, packages);
                var json = await LoadJsonAsync(target, "sleet.packageindex.json");

                // Assert
                // The gallery shows ids as they are written here and takes the first
                // stable version as the latest one without sorting the versions itself.
                var index = (JObject)json["packages"];
                index.Properties().Select(e => e.Name).Should().Equal("Contoso.Core", "Contoso.Meta");
                index["Contoso.Core"].Values<string>().Should().Equal("2.0.0-Beta.1", "1.1.0", "1.0.0");

                // Build metadata is kept, the gallery removes it before it builds a url.
                index["Contoso.Meta"].Values<string>().Should().Equal("1.0.0+build.5");
            }
        }

        [Fact]
        public async Task GivenPushedPackagesVerifyRegistrationHasEveryVersionInlineOldestFirst()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var packages = new[]
                {
                    new TestNupkg("Contoso.Core", "1.1.0"),
                    new TestNupkg("Contoso.Core", "2.0.0-Beta.1"),
                    new TestNupkg("Contoso.Core", "1.0.0"),
                };

                // Act
                await CreateFeedAsync(packagesFolder, target, cache, packages);
                var json = await LoadJsonAsync(target, "registration/contoso.core/index.json");

                // Assert
                // The gallery reads every version of a package from this one file, it
                // does not follow links to registration pages that are not inline.
                var pages = ((JArray)json["items"]).Cast<JObject>().ToList();
                pages.Should().NotBeEmpty();
                pages.Should().OnlyContain(page => page["items"] is JArray);

                pages.SelectMany(page => page["items"])
                    .Select(leaf => leaf["catalogEntry"]["version"].ToString())
                    .Should().Equal("1.0.0", "1.1.0", "2.0.0-Beta.1");
            }
        }

        [Fact]
        public async Task GivenAPackageWithMetadataVerifyRegistrationHasTheFieldsTheGalleryShows()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var package = new TestNupkg()
                {
                    Nuspec = new TestNuspec()
                    {
                        Id = "Contoso.Core",
                        Version = "1.0.0",
                        Title = "Contoso Core",
                        Description = "Core types.",
                        Authors = "Contoso, Fabrikam",
                        Tags = "contoso core",
                        ProjectUrl = "https://example.com/project",
                        LicenseUrl = "https://example.com/license",
                        Icon = "icon.png",
                        Dependencies = new List<PackageDependencyGroup>()
                        {
                            new PackageDependencyGroup(NuGetFramework.Parse("netstandard2.0"), new[] { new PackageDependency("Newtonsoft.Json", VersionRange.Parse("13.0.1")) }),
                            new PackageDependencyGroup(NuGetFramework.Parse("net8.0"), new[] { new PackageDependency("Contoso.Util", VersionRange.Parse("[1.0.0, 2.0.0)")) }),
                        }
                    },
                    Files = new List<TestNupkgFile>()
                    {
                        new("icon.png")
                    }
                };

                // Act
                await CreateFeedAsync(packagesFolder, target, cache, package);
                var entry = await GetCatalogEntryAsync(target, "contoso.core");

                // Assert
                entry["id"].ToString().Should().Be("Contoso.Core");
                entry["version"].ToString().Should().Be("1.0.0");
                entry["title"].ToString().Should().Be("Contoso Core");
                entry["description"].ToString().Should().Be("Core types.");
                entry["authors"].ToString().Should().Be("Contoso, Fabrikam");
                entry["tags"].Values<string>().Should().BeEquivalentTo("contoso", "core");
                entry["projectUrl"].ToString().Should().Be("https://example.com/project");
                entry["licenseUrl"].ToString().Should().Be("https://example.com/license");
                DateTimeOffset.Parse(entry["published"].ToString(), CultureInfo.InvariantCulture).Year.Should().BeGreaterThan(1901);

                // A non empty iconUrl tells the package and version pages that the icon can be requested.
                entry["iconUrl"].ToString().Should().Be(BaseUri + "flatcontainer/contoso.core/1.0.0/icon");

                // Each target framework is a separate group, with the short folder name
                // that the gallery shows as the heading of the group.
                var groups = ((JArray)entry["dependencyGroups"]).Cast<JObject>().ToList();
                groups.Select(g => g["targetFramework"].ToString()).Should().Equal("net8.0", "netstandard2.0");
                groups[0]["dependencies"].Select(d => d["id"] + " " + d["range"]).Should().Equal("Contoso.Util [1.0.0, 2.0.0)");
                groups[1]["dependencies"].Select(d => d["id"] + " " + d["range"]).Should().Equal("Newtonsoft.Json [13.0.1, )");
            }
        }

        [Fact]
        public async Task GivenAPackageWithoutAnIconVerifyRegistrationHasNoIconUrl()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                // The legacy iconUrl points outside of the feed and is not used.
                var package = new TestNupkg()
                {
                    Nuspec = new TestNuspec()
                    {
                        Id = "Contoso.Util",
                        Version = "1.0.0",
                        IconUrl = "https://example.com/icon.png"
                    }
                };

                // Act
                await CreateFeedAsync(packagesFolder, target, cache, package);
                var entry = await GetCatalogEntryAsync(target, "contoso.util");

                // Assert
                // The package and version pages only request an icon when iconUrl has a value.
                ((string)entry["iconUrl"] ?? string.Empty).Should().BeEmpty();
                File.Exists(Path.Combine(target.Root, "flatcontainer", "contoso.util", "1.0.0", "icon")).Should().BeFalse();
            }
        }

        [Fact]
        public async Task GivenAPackageVerifyFlatContainerFilesUseTheLowercaseIdentityVersion()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var package = new TestNupkg()
                {
                    Nuspec = new TestNuspec()
                    {
                        Id = "Contoso.Meta",
                        Version = "1.0.0-Beta+build.5",
                        Icon = "icon.png",
                        Readme = "README.md"
                    },
                    Files = new List<TestNupkgFile>()
                    {
                        new("icon.png"),
                        new("README.md")
                    }
                };

                // Act
                await CreateFeedAsync(packagesFolder, target, cache, package);

                // Assert
                // The gallery builds these paths itself from the package index, lower
                // cased and without build metadata. File names are read back from disk
                // so that the casing is checked on file systems that ignore it.
                var flat = Path.Combine(target.Root, "flatcontainer");
                GetNames(Directory.GetDirectories(flat)).Should().Equal("contoso.meta");
                GetNames(Directory.GetDirectories(Path.Combine(flat, "contoso.meta"))).Should().Equal("1.0.0-beta");

                GetNames(Directory.GetFiles(Path.Combine(flat, "contoso.meta", "1.0.0-beta")))
                    .Should().BeEquivalentTo("contoso.meta.1.0.0-beta.nupkg", "contoso.meta.nuspec", "icon", "readme");

                // Versions in the registration are the ones the gallery links to.
                var entry = await GetCatalogEntryAsync(target, "contoso.meta");
                entry["version"].ToString().Should().Be("1.0.0-Beta");
            }
        }

        private static async Task CreateFeedAsync(TestFolder packagesFolder, TestFolder target, LocalCache cache, params TestNupkg[] packages)
        {
            var log = new TestLogger();
            var settings = new LocalSettings();
            var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root), UriUtility.CreateUri(BaseUri));

            var inputs = packages.Select(e => e.Save(packagesFolder.Root).FullName).ToList();

            var success = await InitCommand.InitAsync(settings, fileSystem, new FeedSettings(), log, CancellationToken.None);
            success &= await PushCommand.RunAsync(settings, fileSystem, inputs, force: false, skipExisting: false, log: log);

            success.Should().BeTrue();
        }

        private static Task<JObject> LoadJsonAsync(TestFolder target, string path)
        {
            return JsonUtility.LoadJsonAsync(Path.Combine(target.Root, path));
        }

        private static async Task<JObject> GetCatalogEntryAsync(TestFolder target, string lowerId)
        {
            var json = await LoadJsonAsync(target, $"registration/{lowerId}/index.json");
            return (JObject)json["items"].Single()["items"].Single()["catalogEntry"];
        }

        private static IEnumerable<string> GetNames(IEnumerable<string> paths)
        {
            return paths.Select(Path.GetFileName);
        }
    }
}
