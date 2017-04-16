using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Helpers;
using NuGet.Versioning;
using Xunit;

namespace Sleet.Test
{
    public class CatalogTests
    {
        [Fact]
        public void CatalogTest_CreatePackageDetails()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                    {
                        CatalogEnabled = true
                    }
                };

                var catalog = new Catalog(context);

                var testPackage = new TestNupkg()
                {
                    Nuspec = new TestNuspec()
                    {
                        Id = "packageA",
                        Version = "1.0.0-alpha.1",
                        Authors = "authorA, authorB",
                        Copyright = "Copyright info",
                        Description = "Package A",
                        IconUrl = "http://tempuri.org/icon.png",
                        LicenseUrl = "http://tempuri.org/license.html",
                        Language = "en-us",
                        MinClientVersion = "3.3.0",
                        DevelopmentDependency = "true",
                        Owners = "ownerA, ownerB",
                        ProjectUrl = "http://tempuri.org/project.html",
                        ReleaseNotes = "release 1.0",
                        RequireLicenseAcceptance = "true",
                        Summary = "package summary.",
                        Tags = "tagA tagB tagC",
                        Title = "packageA title",
                        Dependencies = new List<PackageDependencyGroup>()
                        {
                            new PackageDependencyGroup(NuGetFramework.AnyFramework, new List<PackageDependency>()
                            {
                                new PackageDependency("packageB", VersionRange.Parse("1.0.0"))
                            }),
                            new PackageDependencyGroup(NuGetFramework.Parse("net46"), new List<PackageDependency>()),
                            new PackageDependencyGroup(NuGetFramework.Parse("net45"), new List<PackageDependency>()
                            {
                                new PackageDependency("packageAll"),
                                new PackageDependency("packageExact", VersionRange.Parse("[2.0.0]")),
                            }),
                        },
                        FrameworkAssemblies = new List<KeyValuePair<string, List<NuGetFramework>>>()
                        {
                            new KeyValuePair<string, List<NuGetFramework>>("System.IO.Compression", new List<NuGetFramework>()
                            {
                                NuGetFramework.Parse("net45"),
                                NuGetFramework.Parse("win8")
                            }),
                            new KeyValuePair<string, List<NuGetFramework>>("System.Threading", new List<NuGetFramework>()
                            {
                                NuGetFramework.Parse("net40")
                            }),
                            new KeyValuePair<string, List<NuGetFramework>>("System.All", new List<NuGetFramework>()
                            {
                            })
                        },
                    }
                };

                var zipFile = testPackage.Save(packagesFolder.Root);
                using (var zip = new ZipArchive(File.OpenRead(zipFile.FullName), ZipArchiveMode.Read, false))
                {
                    var input = new PackageInput()
                    {
                        Identity = new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0-alpha.1")),
                        NupkgUri = UriUtility.CreateUri("http://tempuri.org/flatcontainer/packageA/1.0.0-alpha.1/packageA.1.0.0-alpha.1.nupkg"),
                        Zip = zip,
                        Package = new PackageArchiveReader(zip),
                        PackagePath = zipFile.FullName
                    };

                    // Act
                    var actual = CatalogUtility.CreatePackageDetails(input, catalog.CatalogBaseURI, context.CommitId);

                    var dependencyGroups = actual["dependencyGroups"] as JArray;
                    var frameworkAssemblyGroups = actual["frameworkAssemblyGroup"] as JArray;

                    // Assert
                    Assert.True(actual["@id"].ToString().EndsWith(".json"));
                    Assert.Contains("/catalog/data/", actual["@id"].ToString());
                    Assert.Equal(testPackage.Nuspec.Authors, actual["authors"].ToString());
                    Assert.Equal(testPackage.Nuspec.Copyright, actual["copyright"].ToString());
                    Assert.Equal(testPackage.Nuspec.Description, actual["description"].ToString());
                    Assert.Equal(testPackage.Nuspec.IconUrl, actual["iconUrl"].ToString());
                    Assert.Equal(testPackage.Nuspec.LicenseUrl, actual["licenseUrl"].ToString());
                    Assert.Equal(testPackage.Nuspec.MinClientVersion, actual["minClientVersion"].ToString());
                    Assert.Equal(testPackage.Nuspec.ProjectUrl, actual["projectUrl"].ToString());
                    Assert.True(actual["requireLicenseAcceptance"].ToObject<bool>());
                    Assert.Equal(testPackage.Nuspec.Title, actual["title"].ToString());
                    Assert.Equal(testPackage.Nuspec.Id, actual["id"].ToString());
                    Assert.Equal(testPackage.Nuspec.Version, actual["version"].ToString());
                    Assert.Equal("tagA", ((JArray)actual["tags"])[0].ToString());
                    Assert.Equal("tagB", ((JArray)actual["tags"])[1].ToString());
                    Assert.Equal("tagC", ((JArray)actual["tags"])[2].ToString());
                    Assert.True(actual["packageContent"].ToString().EndsWith(".nupkg"));

                    Assert.Null(dependencyGroups[0]["targetFramework"]);
                    Assert.Equal("packageB", ((JArray)dependencyGroups[0]["dependencies"]).Single()["id"]);
                    Assert.Equal("[1.0.0, )", ((JArray)dependencyGroups[0]["dependencies"]).Single()["range"]);

                    Assert.Equal("net45", dependencyGroups[1]["targetFramework"]);
                    Assert.NotNull(dependencyGroups[1]["dependencies"]);

                    Assert.Equal("net46", dependencyGroups[2]["targetFramework"]);
                    Assert.Null(dependencyGroups[2]["dependencies"]);

                    Assert.Null(frameworkAssemblyGroups[0]["targetFramework"]);
                    Assert.Equal("net40", frameworkAssemblyGroups[1]["targetFramework"]);
                    Assert.Equal("net45", frameworkAssemblyGroups[2]["targetFramework"]);
                    Assert.Equal("win8", frameworkAssemblyGroups[3]["targetFramework"]);

                    Assert.Equal("System.All", ((JArray)frameworkAssemblyGroups[0]["assembly"]).Single());
                    Assert.Equal("System.Threading", ((JArray)frameworkAssemblyGroups[1]["assembly"]).Single());
                    Assert.Equal("System.IO.Compression", ((JArray)frameworkAssemblyGroups[2]["assembly"]).Single());
                    Assert.Equal("System.IO.Compression", ((JArray)frameworkAssemblyGroups[3]["assembly"]).Single());
                }
            }
        }

        [Fact]
        public void CatalogTest_CreatePackageDetails_Minimal()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                    {
                        CatalogEnabled = true
                    }
                };

                var catalog = new Catalog(context);
                var testPackage = new TestNupkg("packageA", "1.0.0");

                var zipFile = testPackage.Save(packagesFolder.Root);
                using (var zip = new ZipArchive(File.OpenRead(zipFile.FullName), ZipArchiveMode.Read, false))
                {
                    var input = new PackageInput()
                    {
                        Identity = new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")),
                        NupkgUri = UriUtility.CreateUri("http://tempuri.org/flatcontainer/packageA/1.0.0/packageA.1.0.0.nupkg"),
                        Zip = zip,
                        Package = new PackageArchiveReader(zip),
                        PackagePath = zipFile.FullName
                    };

                    // Act
                    var actual = CatalogUtility.CreatePackageDetails(input, catalog.CatalogBaseURI, context.CommitId);

                    var dependencyGroups = actual["dependencyGroups"] as JArray;
                    var frameworkAssemblyGroups = actual["frameworkAssemblyGroup"] as JArray;
                    var tags = actual["tags"] as JArray;

                    // Assert
                    Assert.True(actual["@id"].ToString().EndsWith(".json"));
                    Assert.Equal(string.Empty, actual["authors"].ToString());
                    Assert.Equal(string.Empty, actual["copyright"].ToString());
                    Assert.Equal(string.Empty, actual["description"].ToString());
                    Assert.Equal(string.Empty, actual["iconUrl"].ToString());
                    Assert.Equal(string.Empty, actual["licenseUrl"].ToString());
                    Assert.Null(actual["minClientVersion"]);
                    Assert.Equal(string.Empty, actual["projectUrl"].ToString());
                    Assert.False(actual["requireLicenseAcceptance"].ToObject<bool>());
                    Assert.Null(actual["title"]);
                    Assert.Equal(testPackage.Nuspec.Id, actual["id"].ToString());
                    Assert.Equal(testPackage.Nuspec.Version, actual["version"].ToString());
                    Assert.True(actual["packageContent"].ToString().EndsWith(".nupkg"));

                    Assert.Equal(0, dependencyGroups.Count);
                    Assert.Equal(0, frameworkAssemblyGroups.Count);
                    Assert.Equal(0, tags.Count);
                }
            }
        }
    }
}