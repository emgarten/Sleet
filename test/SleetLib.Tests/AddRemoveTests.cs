using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Helpers;
using NuGet.Versioning;
using Xunit;

namespace Sleet.Test
{
    public class AddRemoveTests
    {
        [Fact]
        public async Task AddRemove_AddNonNormalizedPackageAsync()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
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

                var testPackage = new TestNupkg("packageA", "1.0");

                var zipFile = testPackage.Save(packagesFolder.Root);
                using (var zip = new ZipArchive(File.OpenRead(zipFile.FullName), ZipArchiveMode.Read, false))
                {
                    var input = new PackageInput(zipFile.FullName, new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), false)
                    {
                        Zip = zip,
                        Package = new PackageArchiveReader(zip)
                    };

                    var catalog = new Catalog(context);
                    var registration = new Registrations(context);
                    var packageIndex = new PackageIndex(context);
                    var search = new Search(context);
                    var autoComplete = new AutoComplete(context);

                    // Act
                    // run commands
                    await InitCommand.InitAsync(context);
                    await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile.FullName }, false, false, context.Log);
                    var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                    // read outputs
                    var catalogEntries = await catalog.GetIndexEntriesAsync();
                    var catalogExistingEntries = await catalog.GetExistingPackagesIndexAsync();
                    var catalogLatest = await catalog.GetLatestEntryAsync(input.Identity);

                    var regPackages = await registration.GetPackagesByIdAsync(input.Identity.Id);
                    var indexPackages = await packageIndex.GetPackagesAsync();
                    var searchPackages = await search.GetPackagesAsync();
                    var autoCompletePackages = await autoComplete.GetPackageIds();

                    // Assert
                    Assert.True(validateOutput);
                    Assert.Equal(1, catalogEntries.Count);
                    Assert.Equal(1, catalogExistingEntries.Count);
                    Assert.Equal(1, regPackages.Count);
                    Assert.Equal(1, indexPackages.Count);
                    Assert.Equal(1, searchPackages.Count);
                    Assert.Equal(1, autoCompletePackages.Count);

                    Assert.Equal("packageA", catalogLatest.Id);
                    Assert.Equal("1.0.0", catalogLatest.Version.ToIdentityString());
                    Assert.Equal(SleetOperation.Add, catalogLatest.Operation);
                }
            }
        }

        // Add and remove a package, verify that the feed is valid and that no packages exist
        [Fact]
        public async Task AddRemove_AddAndRemovePackageAsync()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
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

                var testPackage = new TestNupkg("packageA", "1.0.0");

                var zipFile = testPackage.Save(packagesFolder.Root);
                using (var zip = new ZipArchive(File.OpenRead(zipFile.FullName), ZipArchiveMode.Read, false))
                {
                    var input = new PackageInput(zipFile.FullName, new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), false)
                    {
                        Zip = zip,
                        Package = new PackageArchiveReader(zip)
                    };

                    var catalog = new Catalog(context);
                    var registration = new Registrations(context);
                    var packageIndex = new PackageIndex(context);
                    var search = new Search(context);
                    var autoComplete = new AutoComplete(context);

                    // Act
                    // run commands
                    await InitCommand.InitAsync(context);
                    await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile.FullName }, false, false, context.Log);
                    await DeleteCommand.RunAsync(context.LocalSettings, context.Source, "packageA", "1.0.0", string.Empty, false, context.Log);
                    var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                    // read outputs
                    var catalogEntries = await catalog.GetIndexEntriesAsync();
                    var catalogExistingEntries = await catalog.GetExistingPackagesIndexAsync();
                    var catalogLatest = await catalog.GetLatestEntryAsync(input.Identity);

                    var regPackages = await registration.GetPackagesByIdAsync(input.Identity.Id);
                    var indexPackages = await packageIndex.GetPackagesAsync();
                    var searchPackages = await search.GetPackagesAsync();
                    var autoCompletePackages = await autoComplete.GetPackageIds();

                    // Assert
                    Assert.True(validateOutput);
                    Assert.Equal(2, catalogEntries.Count);
                    Assert.Equal(0, catalogExistingEntries.Count);
                    Assert.Equal(0, regPackages.Count);
                    Assert.Equal(0, indexPackages.Count);
                    Assert.Equal(0, searchPackages.Count);
                    Assert.Equal(0, autoCompletePackages.Count);

                    Assert.Equal("packageA", catalogLatest.Id);
                    Assert.Equal("1.0.0", catalogLatest.Version.ToIdentityString());
                    Assert.Equal(SleetOperation.Remove, catalogLatest.Operation);
                }
            }
        }

        [Fact]
        public async Task AddRemove_AddSinglePackage()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
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

                var testPackage = new TestNupkg("packageA", "1.0.0");

                var zipFile = testPackage.Save(packagesFolder.Root);
                using (var zip = new ZipArchive(File.OpenRead(zipFile.FullName), ZipArchiveMode.Read, false))
                {
                    var input = new PackageInput(zipFile.FullName, new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), false)
                    {
                        Zip = zip,
                        Package = new PackageArchiveReader(zip)
                    };

                    var catalog = new Catalog(context);
                    var registration = new Registrations(context);
                    var packageIndex = new PackageIndex(context);
                    var search = new Search(context);
                    var autoComplete = new AutoComplete(context);

                    // Act
                    // run commands
                    await InitCommand.InitAsync(context);
                    await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile.FullName }, false, false, context.Log);
                    var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                    // read outputs
                    var catalogEntries = await catalog.GetIndexEntriesAsync();
                    var catalogExistingEntries = await catalog.GetExistingPackagesIndexAsync();
                    var catalogLatest = await catalog.GetLatestEntryAsync(input.Identity);

                    var regPackages = await registration.GetPackagesByIdAsync(input.Identity.Id);
                    var indexPackages = await packageIndex.GetPackagesAsync();
                    var searchPackages = await search.GetPackagesAsync();
                    var autoCompletePackages = await autoComplete.GetPackageIds();

                    // Assert
                    Assert.True(validateOutput);
                    Assert.Equal(1, catalogEntries.Count);
                    Assert.Equal(1, catalogExistingEntries.Count);
                    Assert.Equal(1, regPackages.Count);
                    Assert.Equal(1, indexPackages.Count);
                    Assert.Equal(1, searchPackages.Count);
                    Assert.Equal(1, autoCompletePackages.Count);

                    Assert.Equal("packageA", catalogLatest.Id);
                    Assert.Equal("1.0.0", catalogLatest.Version.ToIdentityString());
                    Assert.Equal(SleetOperation.Add, catalogLatest.Operation);
                }
            }
        }

        [Fact]
        public async Task GivenThatIAddAPackageWithTheCatalogDisabledVerifyItSucceeds()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
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

                context.SourceSettings.CatalogEnabled = false;

                var testPackage = new TestNupkg("packageA", "1.0.0");

                var zipFile = testPackage.Save(packagesFolder.Root);
                using (var zip = new ZipArchive(File.OpenRead(zipFile.FullName), ZipArchiveMode.Read, false))
                {
                    var input = new PackageInput(zipFile.FullName, new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), false)
                    {
                        Zip = zip,
                        Package = new PackageArchiveReader(zip)
                    };

                    var catalog = new Catalog(context);
                    var registration = new Registrations(context);
                    var packageIndex = new PackageIndex(context);
                    var search = new Search(context);
                    var autoComplete = new AutoComplete(context);

                    // Act
                    // run commands
                    await InitCommand.InitAsync(context);
                    await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile.FullName }, false, false, context.Log);
                    var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                    // read outputs
                    var catalogEntries = await catalog.GetIndexEntriesAsync();
                    var catalogExistingEntries = await catalog.GetExistingPackagesIndexAsync();
                    var catalogLatest = await catalog.GetLatestEntryAsync(input.Identity);

                    var regPackages = await registration.GetPackagesByIdAsync(input.Identity.Id);
                    var indexPackages = await packageIndex.GetPackagesAsync();
                    var searchPackages = await search.GetPackagesAsync();
                    var autoCompletePackages = await autoComplete.GetPackageIds();

                    var catalogEntry = await registration.GetCatalogEntryFromPackageBlob(input.Identity);

                    // Assert
                    validateOutput.Should().BeTrue("the feed is valid");
                    catalogEntries.Should().BeEmpty("the catalog is disabled");
                    catalogExistingEntries.Should().BeEmpty("the catalog is disabled");
                    regPackages.Should().BeEquivalentTo(new[] { input.Identity });
                    indexPackages.Should().BeEquivalentTo(new[] { input.Identity });
                    searchPackages.Should().BeEquivalentTo(new[] { input.Identity });
                    autoCompletePackages.Should().BeEquivalentTo(new[] { input.Identity.Id });

                    catalogLatest.Should().BeNull();
                    catalogEntry["version"].ToString().Should().Be("1.0.0");
                    catalogEntry["sleet:operation"].ToString().Should().Be("add");
                }
            }
        }

        [Fact]
        public async Task GivenThatIRemoveAPackageWithTheCatalogDisabledVerifyItSucceeds()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
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

                context.SourceSettings.CatalogEnabled = false;

                var testPackage1 = new TestNupkg("packageA", "1.0.1");
                var testPackage2 = new TestNupkg("packageA", "1.0.2");
                var testPackage3 = new TestNupkg("packageA", "1.0.3");

                var zipFile1 = testPackage1.Save(packagesFolder.Root);
                var zipFile2 = testPackage2.Save(packagesFolder.Root);
                var zipFile3 = testPackage3.Save(packagesFolder.Root);

                var catalog = new Catalog(context);
                var registration = new Registrations(context);
                var packageIndex = new PackageIndex(context);
                var search = new Search(context);
                var autoComplete = new AutoComplete(context);

                // Act
                // run commands
                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile1.FullName }, false, false, context.Log);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile2.FullName }, false, false, context.Log);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile3.FullName }, false, false, context.Log);
                await DeleteCommand.RunAsync(context.LocalSettings, context.Source, "packageA", "1.0.3", "", false, context.Log);
                await DeleteCommand.RunAsync(context.LocalSettings, context.Source, "packageA", "1.0.1", "", false, context.Log);

                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                // read outputs
                var catalogEntries = await catalog.GetIndexEntriesAsync();
                var catalogExistingEntries = await catalog.GetExistingPackagesIndexAsync();

                var regPackages = await registration.GetPackagesByIdAsync("packageA");
                var indexPackages = await packageIndex.GetPackagesAsync();
                var searchPackages = await search.GetPackagesAsync();
                var autoCompletePackages = await autoComplete.GetPackageIds();

                var catalogEntry = await registration.GetCatalogEntryFromPackageBlob(new PackageIdentity("packageA", NuGetVersion.Parse("1.0.2")));

                // Assert
                validateOutput.Should().BeTrue("the feed is valid");
                catalogEntries.Should().BeEmpty("the catalog is disabled");
                catalogExistingEntries.Should().BeEmpty("the catalog is disabled");
                regPackages.Should().BeEquivalentTo(new[] { new PackageIdentity("packageA", NuGetVersion.Parse("1.0.2")) });
                indexPackages.Should().BeEquivalentTo(new[] { new PackageIdentity("packageA", NuGetVersion.Parse("1.0.2")) });
                searchPackages.Should().BeEquivalentTo(new[] { new PackageIdentity("packageA", NuGetVersion.Parse("1.0.2")) });
                autoCompletePackages.Should().BeEquivalentTo(new[] { "packageA" });
                catalogEntry["version"].ToString().Should().Be("1.0.2");
                catalogEntry["sleet:operation"].ToString().Should().Be("add");
            }
        }

        [Fact]
        public async Task GivenThatIRemoveAllPackagesWithTheCatalogDisabledVerifyItSucceeds()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
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

                context.SourceSettings.CatalogEnabled = false;

                var testPackage1 = new TestNupkg("packageA", "1.0.1");
                var testPackage2 = new TestNupkg("packageA", "1.0.2");
                var testPackage3 = new TestNupkg("packageA", "1.0.3");

                var zipFile1 = testPackage1.Save(packagesFolder.Root);
                var zipFile2 = testPackage2.Save(packagesFolder.Root);
                var zipFile3 = testPackage3.Save(packagesFolder.Root);

                var catalog = new Catalog(context);
                var registration = new Registrations(context);
                var packageIndex = new PackageIndex(context);
                var search = new Search(context);
                var autoComplete = new AutoComplete(context);

                // Act
                // run commands
                await InitCommand.InitAsync(context);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile1.FullName }, false, false, context.Log);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile2.FullName }, false, false, context.Log);
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile3.FullName }, false, false, context.Log);
                await DeleteCommand.RunAsync(context.LocalSettings, context.Source, "packageA", "1.0.3", "", false, context.Log);
                await DeleteCommand.RunAsync(context.LocalSettings, context.Source, "packageA", "1.0.1", "", false, context.Log);
                await DeleteCommand.RunAsync(context.LocalSettings, context.Source, "packageA", "1.0.2", "", false, context.Log);

                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                // read outputs
                var catalogEntries = await catalog.GetIndexEntriesAsync();
                var catalogExistingEntries = await catalog.GetExistingPackagesIndexAsync();

                var regPackages = await registration.GetPackagesByIdAsync("packageA");
                var indexPackages = await packageIndex.GetPackagesAsync();
                var searchPackages = await search.GetPackagesAsync();
                var autoCompletePackages = await autoComplete.GetPackageIds();

                // Assert
                validateOutput.Should().BeTrue("the feed is valid");
                catalogEntries.Should().BeEmpty("the catalog is disabled");
                catalogExistingEntries.Should().BeEmpty("the catalog is disabled");
                regPackages.Should().BeEmpty("all packages were removed");
                indexPackages.Should().BeEmpty("all packages were removed");
                searchPackages.Should().BeEmpty("all packages were removed");
                autoCompletePackages.Should().BeEmpty("all packages were removed");
            }
        }

        [Fact]
        public async Task AddRemove_AddTwoPackagesOfSameId()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
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

                var testPackage1 = new TestNupkg("packageA", "1.0.0");
                var testPackage2 = new TestNupkg("packageA", "2.0.0");

                var zipFile1 = testPackage1.Save(packagesFolder.Root);
                var zipFile2 = testPackage2.Save(packagesFolder.Root);
                using (var zip1 = new ZipArchive(File.OpenRead(zipFile1.FullName), ZipArchiveMode.Read, false))
                using (var zip2 = new ZipArchive(File.OpenRead(zipFile2.FullName), ZipArchiveMode.Read, false))
                {
                    var input = new PackageInput(zipFile1.FullName, new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), false)
                    {
                        Zip = zip1,
                        Package = new PackageArchiveReader(zip1)
                    };

                    var input2 = new PackageInput(zipFile2.FullName, new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), false)
                    {
                        Zip = zip2,
                        Package = new PackageArchiveReader(zip2)
                    };

                    var catalog = new Catalog(context);
                    var registration = new Registrations(context);
                    var packageIndex = new PackageIndex(context);
                    var search = new Search(context);
                    var autoComplete = new AutoComplete(context);

                    // Act
                    // run commands
                    await InitCommand.InitAsync(context);
                    await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile1.FullName }, false, false, context.Log);
                    await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile2.FullName }, false, false, context.Log);
                    var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                    // read outputs
                    var catalogEntries = await catalog.GetIndexEntriesAsync();
                    var catalogExistingEntries = await catalog.GetExistingPackagesIndexAsync();

                    var regPackages = await registration.GetPackagesByIdAsync("packageA");
                    var indexPackages = await packageIndex.GetPackagesAsync();
                    var searchPackages = await search.GetPackagesAsync();
                    var autoCompletePackages = await autoComplete.GetPackageIds();

                    // Assert
                    Assert.True(validateOutput);
                    Assert.Equal(2, catalogEntries.Count);
                    Assert.Equal(2, catalogExistingEntries.Count);
                    Assert.Equal(2, regPackages.Count);
                    Assert.Equal(2, indexPackages.Count);
                    Assert.Equal(2, searchPackages.Count);
                    Assert.Equal(1, autoCompletePackages.Count);
                }
            }
        }

        [Fact]
        public async Task AddRemove_AddTwoPackagesOfUniqueIds()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
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

                var testPackage1 = new TestNupkg("packageA", "1.0.0");
                var testPackage2 = new TestNupkg("packageB", "1.0.0");

                var zipFile1 = testPackage1.Save(packagesFolder.Root);
                var zipFile2 = testPackage2.Save(packagesFolder.Root);
                using (var zip1 = new ZipArchive(File.OpenRead(zipFile1.FullName), ZipArchiveMode.Read, false))
                using (var zip2 = new ZipArchive(File.OpenRead(zipFile2.FullName), ZipArchiveMode.Read, false))
                {
                    var input1 = new PackageInput(zipFile1.FullName, new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), false)
                    {
                        Zip = zip1,
                        Package = new PackageArchiveReader(zip1)
                    };

                    var input2 = new PackageInput(zipFile2.FullName, new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), false)
                    {
                        Zip = zip2,
                        Package = new PackageArchiveReader(zip2)
                    };

                    var catalog = new Catalog(context);
                    var registration = new Registrations(context);
                    var packageIndex = new PackageIndex(context);
                    var search = new Search(context);
                    var autoComplete = new AutoComplete(context);

                    // Act
                    // run commands
                    await InitCommand.InitAsync(context);
                    await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile1.FullName }, false, false, context.Log);
                    await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile2.FullName }, false, false, context.Log);
                    var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                    // read outputs
                    var catalogEntries = await catalog.GetIndexEntriesAsync();
                    var catalogExistingEntries = await catalog.GetExistingPackagesIndexAsync();

                    var regPackages = await registration.GetPackagesByIdAsync("packageA");
                    var indexPackages = await packageIndex.GetPackagesAsync();
                    var searchPackages = await search.GetPackagesAsync();
                    var autoCompletePackages = await autoComplete.GetPackageIds();

                    // Assert
                    Assert.True(validateOutput);
                    Assert.Equal(2, catalogEntries.Count);
                    Assert.Equal(2, catalogExistingEntries.Count);
                    Assert.Equal(1, regPackages.Count);
                    Assert.Equal(2, indexPackages.Count);
                    Assert.Equal(2, searchPackages.Count);
                    Assert.Equal(2, autoCompletePackages.Count);
                }
            }
        }
    }
}