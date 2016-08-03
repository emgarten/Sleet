using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace Sleet.Test
{
    public class AddRemoveTests
    {
        [Fact]
        public async Task AddRemove_AddNonNormalizedPackage()
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

                var testPackage = new TestPackageContext()
                {
                    Nuspec = new TestNuspecContext()
                    {
                        Id = "packageA",
                        Version = "1.0"
                    }
                };

                var zipFile = testPackage.Create(packagesFolder.Root);
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
                    await InitCommandTestHook.RunCore(context.LocalSettings, context.Source, context.Log);
                    await PushCommandTestHook.RunCore(context.LocalSettings, context.Source, new List<string>() { zipFile.FullName }, false, context.Log);
                    var validateOutput = await ValidateCommandTestHook.RunCore(context.LocalSettings, context.Source, context.Log);

                    // read outputs
                    var catalogEntries = await catalog.GetIndexEntries();
                    var catalogExistingEntries = await catalog.GetExistingPackagesIndex();
                    var catalogLatest = await catalog.GetLatestEntry(input.Identity);

                    var regPackages = await registration.GetPackagesById(input.Identity.Id);
                    var indexPackages = await packageIndex.GetPackages();
                    var searchPackages = await search.GetPackages();
                    var autoCompletePackages = await autoComplete.GetPackageIds();

                    // Assert
                    Assert.Equal(0, validateOutput);
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
        public async Task AddRemove_AddAndRemovePackage()
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

                var testPackage = new TestPackageContext()
                {
                    Nuspec = new TestNuspecContext()
                    {
                        Id = "packageA",
                        Version = "1.0.0"
                    }
                };

                var zipFile = testPackage.Create(packagesFolder.Root);
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
                    await InitCommandTestHook.RunCore(context.LocalSettings, context.Source, context.Log);
                    await PushCommandTestHook.RunCore(context.LocalSettings, context.Source, new List<string>() { zipFile.FullName }, false, context.Log);
                    await DeleteCommandTestHook.RunCore(context.LocalSettings, context.Source, "packageA", "1.0.0", string.Empty, false, context.Log);
                    var validateOutput = await ValidateCommandTestHook.RunCore(context.LocalSettings, context.Source, context.Log);

                    // read outputs
                    var catalogEntries = await catalog.GetIndexEntries();
                    var catalogExistingEntries = await catalog.GetExistingPackagesIndex();
                    var catalogLatest = await catalog.GetLatestEntry(input.Identity);

                    var regPackages = await registration.GetPackagesById(input.Identity.Id);
                    var indexPackages = await packageIndex.GetPackages();
                    var searchPackages = await search.GetPackages();
                    var autoCompletePackages = await autoComplete.GetPackageIds();

                    // Assert
                    Assert.Equal(0, validateOutput);
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

                var testPackage = new TestPackageContext()
                {
                    Nuspec = new TestNuspecContext()
                    {
                        Id = "packageA",
                        Version = "1.0.0"
                    }
                };

                var zipFile = testPackage.Create(packagesFolder.Root);
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
                    await InitCommandTestHook.RunCore(context.LocalSettings, context.Source, context.Log);
                    await PushCommandTestHook.RunCore(context.LocalSettings, context.Source, new List<string>() { zipFile.FullName }, false, context.Log);
                    var validateOutput = await ValidateCommandTestHook.RunCore(context.LocalSettings, context.Source, context.Log);

                    // read outputs
                    var catalogEntries = await catalog.GetIndexEntries();
                    var catalogExistingEntries = await catalog.GetExistingPackagesIndex();
                    var catalogLatest = await catalog.GetLatestEntry(input.Identity);

                    var regPackages = await registration.GetPackagesById(input.Identity.Id);
                    var indexPackages = await packageIndex.GetPackages();
                    var searchPackages = await search.GetPackages();
                    var autoCompletePackages = await autoComplete.GetPackageIds();

                    // Assert
                    Assert.Equal(0, validateOutput);
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

                var testPackage1 = new TestPackageContext()
                {
                    Nuspec = new TestNuspecContext()
                    {
                        Id = "packageA",
                        Version = "1.0.0"
                    }
                };

                var testPackage2 = new TestPackageContext()
                {
                    Nuspec = new TestNuspecContext()
                    {
                        Id = "packageA",
                        Version = "2.0.0"
                    }
                };

                var zipFile1 = testPackage1.Create(packagesFolder.Root);
                var zipFile2 = testPackage2.Create(packagesFolder.Root);
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
                    await InitCommandTestHook.RunCore(context.LocalSettings, context.Source, context.Log);
                    await PushCommandTestHook.RunCore(context.LocalSettings, context.Source, new List<string>() { zipFile1.FullName }, false, context.Log);
                    await PushCommandTestHook.RunCore(context.LocalSettings, context.Source, new List<string>() { zipFile2.FullName }, false, context.Log);
                    var validateOutput = await ValidateCommandTestHook.RunCore(context.LocalSettings, context.Source, context.Log);

                    // read outputs
                    var catalogEntries = await catalog.GetIndexEntries();
                    var catalogExistingEntries = await catalog.GetExistingPackagesIndex();

                    var regPackages = await registration.GetPackagesById("packageA");
                    var indexPackages = await packageIndex.GetPackages();
                    var searchPackages = await search.GetPackages();
                    var autoCompletePackages = await autoComplete.GetPackageIds();

                    // Assert
                    Assert.Equal(0, validateOutput);
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

                var testPackage1 = new TestPackageContext()
                {
                    Nuspec = new TestNuspecContext()
                    {
                        Id = "packageA",
                        Version = "1.0.0"
                    }
                };

                var testPackage2 = new TestPackageContext()
                {
                    Nuspec = new TestNuspecContext()
                    {
                        Id = "packageB",
                        Version = "1.0.0"
                    }
                };

                var zipFile1 = testPackage1.Create(packagesFolder.Root);
                var zipFile2 = testPackage2.Create(packagesFolder.Root);
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
                    await InitCommandTestHook.RunCore(context.LocalSettings, context.Source, context.Log);
                    await PushCommandTestHook.RunCore(context.LocalSettings, context.Source, new List<string>() { zipFile1.FullName }, false, context.Log);
                    await PushCommandTestHook.RunCore(context.LocalSettings, context.Source, new List<string>() { zipFile2.FullName }, false, context.Log);
                    var validateOutput = await ValidateCommandTestHook.RunCore(context.LocalSettings, context.Source, context.Log);

                    // read outputs
                    var catalogEntries = await catalog.GetIndexEntries();
                    var catalogExistingEntries = await catalog.GetExistingPackagesIndex();

                    var regPackages = await registration.GetPackagesById("packageA");
                    var indexPackages = await packageIndex.GetPackages();
                    var searchPackages = await search.GetPackages();
                    var autoCompletePackages = await autoComplete.GetPackageIds();

                    // Assert
                    Assert.Equal(0, validateOutput);
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
