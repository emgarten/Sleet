using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;
using Sleet.Test;
using Xunit;

namespace Sleet.Integration.Test
{
    public class NuGetReaderTests
    {
        // TODO:
        // Verify all packages are found for dependency info resource, when multiple pages exist
        // Verify latest is found with metadata resource
        // Verify flat container returns all versions
        // Verify download resource
        // Verify auto complete resource
        // Verify search

        [Fact]
        public async Task NuGetReader_DependencyInfoResource_DependencyGroups()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var outputRoot = Path.Combine(target.Root, "output");
                var baseUri = new Uri("https://localhost:8080/testFeed/");

                var log = new TestLogger();

                var testPackage = new TestPackageContext()
                {
                    Nuspec = new TestNuspecContext()
                    {
                        Id = "packageA",
                        Version = "1.0.0",
                        Dependencies = new List<PackageDependencyGroup>()
                        {
                            new PackageDependencyGroup(NuGetFramework.Parse("net46"),  new List<PackageDependency>() { }),
                            new PackageDependencyGroup(NuGetFramework.Parse("net45"), new[] { new PackageDependency("packageB", VersionRange.Parse("1.0.0")), new PackageDependency("packageC", VersionRange.Parse("2.0.0")) }),
                            new PackageDependencyGroup(NuGetFramework.Parse("any"), new List<PackageDependency>() { new PackageDependency("packageB", VersionRange.Parse("1.0.0")) })
                        }
                    }
                };

                var sleetConfig = TestUtility.CreateConfigWithLocal("local", outputRoot, baseUri.AbsoluteUri);

                var sleetConfigPath = Path.Combine(target.Root, "sleet.config");
                JsonUtility.SaveJson(new FileInfo(sleetConfigPath), sleetConfig);

                var zipFile = testPackage.Create(packagesFolder.Root);

                // Act
                // Run sleet
                var exitCode = await Program.MainCore(new[] { "init", "-c", sleetConfigPath, "-s", "local" }, log);
                exitCode += await Program.MainCore(new[] { "push", zipFile.FullName, "-c", sleetConfigPath, "-s", "local" }, log);

                // Create a repository abstraction for nuget
                var fileSystem = new PhysicalFileSystem(cache, new Uri(outputRoot), baseUri);
                var localSource = GetSource(outputRoot, baseUri, fileSystem);

                var dependencyInfoResource = await localSource.GetResourceAsync<DependencyInfoResource>();

                var dependencyPackagesNet46 = await dependencyInfoResource.ResolvePackages("packageA", NuGetFramework.Parse("net46"), log, CancellationToken.None);
                var dependencyPackageNet46 = dependencyPackagesNet46.Single();
                var depString46 = string.Join("|", dependencyPackageNet46.Dependencies.Select(d => d.Id + " " + d.VersionRange.ToNormalizedString()));

                var dependencyPackagesNet45 = await dependencyInfoResource.ResolvePackages("packageA", NuGetFramework.Parse("net45"), log, CancellationToken.None);
                var dependencyPackageNet45 = dependencyPackagesNet45.Single();
                var depString45 = string.Join("|", dependencyPackageNet45.Dependencies.Select(d => d.Id + " " + d.VersionRange.ToNormalizedString()));

                var dependencyPackagesNet40 = await dependencyInfoResource.ResolvePackages("packageA", NuGetFramework.Parse("net40"), log, CancellationToken.None);
                var dependencyPackageNet40 = dependencyPackagesNet40.Single();
                var depString40 = string.Join("|", dependencyPackageNet40.Dependencies.Select(d => d.Id + " " + d.VersionRange.ToNormalizedString()));

                // Assert
                Assert.True(0 == exitCode, log.ToString());

                Assert.Equal("https://localhost:8080/testFeed/flatcontainer/packagea/1.0.0/packagea.1.0.0.nupkg", dependencyPackageNet46.DownloadUri.AbsoluteUri);
                Assert.Equal(true, dependencyPackageNet46.Listed);
                Assert.Equal("packageA", dependencyPackageNet46.Id);
                Assert.Equal("1.0.0", dependencyPackageNet46.Version.ToNormalizedString());
                Assert.Equal("", depString46);
                Assert.Equal("packageB [1.0.0, )|packageC [2.0.0, )", depString45);
                Assert.Equal("packageB [1.0.0, )", depString40);
            }
        }

        public static SourceRepository GetSource(string outputRoot, Uri baseUri, PhysicalFileSystem fileSystem)
        {
            var providers = Repository.Provider.GetCoreV3().ToList();

            // HttpSource -> PhysicalFileSystem adapter
            providers.Add(new Lazy<INuGetResourceProvider>(() => new TestHttpSourceResourceProvider(fileSystem)));

            return new SourceRepository(new PackageSource(baseUri.AbsoluteUri + "index.json"), providers);
        }
    }
}
