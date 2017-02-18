using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
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
                    SourceSettings = new SourceSettings()
                };

                var testPackage = new TestNupkg("packageA", "1.0");

                var zipFile = testPackage.Save(packagesFolder.Root);
                using (var zip = new ZipArchive(File.OpenRead(zipFile.FullName), ZipArchiveMode.Read, false))
                {
                    var input = new PackageInput()
                    {
                        Identity = new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")),
                        Zip = zip,
                        Package = new PackageArchiveReader(zip),
                        PackagePath = zipFile.FullName
                    };

                    var catalog = new Catalog(context);
                    var registration = new Registrations(context);
                    var packageIndex = new PackageIndex(context);
                    var search = new Search(context);
                    var autoComplete = new AutoComplete(context);

                    // Act
                    // run commands
                    await InitCommand.RunAsync(context.LocalSettings, context.Source, context.Log);
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
                    SourceSettings = new SourceSettings()
                };

                var testPackage = new TestNupkg("packageA", "1.0.0");

                var zipFile = testPackage.Save(packagesFolder.Root);
                using (var zip = new ZipArchive(File.OpenRead(zipFile.FullName), ZipArchiveMode.Read, false))
                {
                    var input = new PackageInput()
                    {
                        Identity = new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")),
                        Zip = zip,
                        Package = new PackageArchiveReader(zip),
                        PackagePath = zipFile.FullName
                    };

                    var catalog = new Catalog(context);
                    var registration = new Registrations(context);
                    var packageIndex = new PackageIndex(context);
                    var search = new Search(context);
                    var autoComplete = new AutoComplete(context);

                    // Act
                    // run commands
                    await InitCommand.RunAsync(context.LocalSettings, context.Source, context.Log);
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
                    SourceSettings = new SourceSettings()
                };

                var testPackage = new TestNupkg("packageA", "1.0.0");

                var zipFile = testPackage.Save(packagesFolder.Root);
                using (var zip = new ZipArchive(File.OpenRead(zipFile.FullName), ZipArchiveMode.Read, false))
                {
                    var input = new PackageInput()
                    {
                        Identity = new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")),
                        Zip = zip,
                        Package = new PackageArchiveReader(zip),
                        PackagePath = zipFile.FullName
                    };

                    var catalog = new Catalog(context);
                    var registration = new Registrations(context);
                    var packageIndex = new PackageIndex(context);
                    var search = new Search(context);
                    var autoComplete = new AutoComplete(context);

                    // Act
                    // run commands
                    await InitCommand.RunAsync(context.LocalSettings, context.Source, context.Log);
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
                    SourceSettings = new SourceSettings()
                };

                var testPackage1 = new TestNupkg("packageA", "1.0.0");
                var testPackage2 = new TestNupkg("packageA", "2.0.0");

                var zipFile1 = testPackage1.Save(packagesFolder.Root);
                var zipFile2 = testPackage2.Save(packagesFolder.Root);
                using (var zip1 = new ZipArchive(File.OpenRead(zipFile1.FullName), ZipArchiveMode.Read, false))
                using (var zip2 = new ZipArchive(File.OpenRead(zipFile2.FullName), ZipArchiveMode.Read, false))
                {
                    var input1 = new PackageInput()
                    {
                        Identity = new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")),
                        Zip = zip1,
                        Package = new PackageArchiveReader(zip1),
                        PackagePath = zipFile1.FullName
                    };

                    var input2 = new PackageInput()
                    {
                        Identity = new PackageIdentity("packageA", NuGetVersion.Parse("2.0.0")),
                        Zip = zip2,
                        Package = new PackageArchiveReader(zip2),
                        PackagePath = zipFile2.FullName
                    };

                    var catalog = new Catalog(context);
                    var registration = new Registrations(context);
                    var packageIndex = new PackageIndex(context);
                    var search = new Search(context);
                    var autoComplete = new AutoComplete(context);

                    // Act
                    // run commands
                    await InitCommand.RunAsync(context.LocalSettings, context.Source, context.Log);
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
                    SourceSettings = new SourceSettings()
                };

                var testPackage1 = new TestNupkg("packageA", "1.0.0");
                var testPackage2 = new TestNupkg("packageB", "1.0.0");

                var zipFile1 = testPackage1.Save(packagesFolder.Root);
                var zipFile2 = testPackage2.Save(packagesFolder.Root);
                using (var zip1 = new ZipArchive(File.OpenRead(zipFile1.FullName), ZipArchiveMode.Read, false))
                using (var zip2 = new ZipArchive(File.OpenRead(zipFile2.FullName), ZipArchiveMode.Read, false))
                {
                    var input1 = new PackageInput()
                    {
                        Identity = new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")),
                        Zip = zip1,
                        Package = new PackageArchiveReader(zip1),
                        PackagePath = zipFile1.FullName
                    };

                    var input2 = new PackageInput()
                    {
                        Identity = new PackageIdentity("packageB", NuGetVersion.Parse("1.0.0")),
                        Zip = zip2,
                        Package = new PackageArchiveReader(zip2),
                        PackagePath = zipFile2.FullName
                    };

                    var catalog = new Catalog(context);
                    var registration = new Registrations(context);
                    var packageIndex = new PackageIndex(context);
                    var search = new Search(context);
                    var autoComplete = new AutoComplete(context);

                    // Act
                    // run commands
                    await InitCommand.RunAsync(context.LocalSettings, context.Source, context.Log);
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