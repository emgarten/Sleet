using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using NuGet.Packaging.Core;
using NuGet.Test.Helpers;
using NuGet.Versioning;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class PushCommandTests
    {
        [Fact]
        public async Task PushCommand_GivenADifferentNuspecCasingVerifyPush()
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

                using (var tempZip = new ZipArchive(zipFile.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None), ZipArchiveMode.Update))
                {
                    var nuspec = tempZip.Entries.Single(e => e.FullName == "packageA.nuspec");

                    using (var ms = new MemoryStream())
                    {
                        using (var nuspecStream = nuspec.Open())
                        {
                            nuspecStream.CopyTo(ms);
                        }
                        ms.Position = 0;

                        nuspec.Delete();
                        var newEntry = tempZip.CreateEntry("PacKAGEa.NuSpec");
                        ms.CopyTo(newEntry.Open());
                    }
                }

                using (var zip = new ZipArchive(File.OpenRead(zipFile.FullName), ZipArchiveMode.Read, false))
                {
                    var input = PackageInput.Create(zipFile.FullName);

                    // Act
                    // run commands
                    await InitCommand.InitAsync(context);
                    await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile.FullName }, false, false, context.Log);
                    var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                    // read outputs
                    var catalog = new Catalog(context);
                    var registration = new Registrations(context);
                    var packageIndex = new PackageIndex(context);
                    var search = new Search(context);
                    var autoComplete = new AutoComplete(context);

                    var catalogEntries = await catalog.GetIndexEntriesAsync();
                    var indexPackages = await packageIndex.GetPackagesAsync();

                    // Assert
                    Assert.True(validateOutput);
                    Assert.Single(catalogEntries);
                    Assert.Single(indexPackages);
                }
            }
        }

        [Fact]
        public async Task PushCommand_GivenANonExistantFeedVerifyAutoInit()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var root = Path.Combine(target.Root, "a/b/feed");
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(root));
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
                var packageIdentity = new PackageIdentity(testPackage.Nuspec.Id, NuGetVersion.Parse(testPackage.Nuspec.Version));

                var zipFile = testPackage.Save(packagesFolder.Root);

                // Act
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile.FullName }, false, false, context.Log);
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                // read outputs
                var packageIndex = new PackageIndex(context);
                var indexPackages = await packageIndex.GetPackagesAsync();

                // Assert
                Assert.Single(indexPackages);
            }
        }

        [Fact]
        public async Task PushCommand_GivenAEmptyFolderVerifyAutoInit()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var root = Path.Combine(target.Root, "a/b/feed");
                Directory.CreateDirectory(root);

                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(root));
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
                var packageIdentity = new PackageIdentity(testPackage.Nuspec.Id, NuGetVersion.Parse(testPackage.Nuspec.Version));

                var zipFile = testPackage.Save(packagesFolder.Root);

                // Act
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile.FullName }, false, false, context.Log);
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                // read outputs
                var packageIndex = new PackageIndex(context);
                var indexPackages = await packageIndex.GetPackagesAsync();

                // Assert
                Assert.Single(indexPackages);
            }
        }

        [Fact]
        public async Task PushCommand_GivenSkipExistingVerifyExistingSkippedAndNewPackageAdded()
        {
            using (var firstPackagesFolder = new TestFolder())
            using (var secondPackagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                await InitCommand.RunAsync(settings, fileSystem, enableCatalog: true, enableSymbols: false, log: log, token: TestContext.Current.CancellationToken);
                new TestNupkg("a", "1.0.0").Save(firstPackagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { firstPackagesFolder.Root }, false, false, log);

                new TestNupkg("a", "1.0.0").Save(secondPackagesFolder.Root);
                new TestNupkg("b", "1.0.0").Save(secondPackagesFolder.Root);

                var success = await PushCommand.RunAsync(settings, fileSystem, new List<string>() { secondPackagesFolder.Root }, false, true, log);

                var packageIndex = new PackageIndex(new SleetContext()
                {
                    Token = TestContext.Current.CancellationToken,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = await FeedSettingsUtility.GetSettingsOrDefault(fileSystem, log, TestContext.Current.CancellationToken)
                });
                var packages = await packageIndex.GetPackagesAsync();

                success.Should().BeTrue();
                packages.Count.Should().Be(2);
                log.GetMessages().Should().Contain("Skip existing package: a 1.0.0");
                log.GetMessages().Should().Contain("Add new package: b 1.0.0");
            }
        }

        [Fact]
        public async Task PushCommand_GivenExistingPackageWithoutForceVerifyThrowsAndFeedUnchanged()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                await InitCommand.RunAsync(settings, fileSystem, enableCatalog: true, enableSymbols: false, log: log, token: TestContext.Current.CancellationToken);
                var package = new TestNupkg("a", "1.0.0").Save(packagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { package.FullName }, false, false, log);

                Func<Task> action = async () => await PushCommand.RunAsync(settings, fileSystem, new List<string>() { package.FullName }, false, false, log);

                await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Package already exists: a 1.0.0.");

                var packageIndex = new PackageIndex(new SleetContext()
                {
                    Token = TestContext.Current.CancellationToken,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = await FeedSettingsUtility.GetSettingsOrDefault(fileSystem, log, TestContext.Current.CancellationToken)
                });
                var packages = await packageIndex.GetPackagesAsync();

                packages.Should().ContainSingle();
            }
        }

        [Fact]
        public async Task PushCommand_GivenExistingPackageWithForceVerifyReplaced()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                await InitCommand.RunAsync(settings, fileSystem, enableCatalog: true, enableSymbols: false, log: log, token: TestContext.Current.CancellationToken);
                var package = new TestNupkg("a", "1.0.0").Save(packagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { package.FullName }, false, false, log);

                var success = await PushCommand.RunAsync(settings, fileSystem, new List<string>() { package.FullName }, true, false, log);
                var validateOutput = await ValidateCommand.RunAsync(settings, fileSystem, log);

                var packageIndex = new PackageIndex(new SleetContext()
                {
                    Token = TestContext.Current.CancellationToken,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = await FeedSettingsUtility.GetSettingsOrDefault(fileSystem, log, TestContext.Current.CancellationToken)
                });
                var packages = await packageIndex.GetPackagesAsync();

                success.Should().BeTrue();
                validateOutput.Should().BeTrue();
                packages.Should().ContainSingle();
                log.GetMessages().Should().Contain("Replace existing package: a 1.0.0");
            }
        }

        [Fact]
        public async Task PushCommand_GivenEmptyInputListVerifyNoPackagesFound()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                Func<Task> action = async () => await PushCommand.RunAsync(settings, fileSystem, new List<string>(), false, false, log);

                await action.Should().ThrowAsync<ArgumentException>().WithMessage("No packages found.");
            }
        }

        [Fact]
        public async Task PushCommand_GivenEmptyDirectoryVerifyThrowsFileNotFound()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                Func<Task> action = async () => await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);

                await action.Should().ThrowAsync<FileNotFoundException>().WithMessage($"Unable to find nupkgs in '{Path.GetFullPath(packagesFolder.Root)}'.");
            }
        }

        [Fact]
        public async Task PushCommand_GivenMissingPathVerifyThrowsFileNotFound()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();
                var missing = Path.Combine(target.Root, "missing.nupkg");

                Func<Task> action = async () => await PushCommand.RunAsync(settings, fileSystem, new List<string>() { missing }, false, false, log);

                await action.Should().ThrowAsync<FileNotFoundException>().WithMessage($"Unable to find '{Path.GetFullPath(missing)}'.");
            }
        }

        [Fact]
        public async Task PushCommand_GivenDuplicatePackageIdentitiesVerifyThrows()
        {
            using (var firstPackagesFolder = new TestFolder())
            using (var secondPackagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();
                var first = new TestNupkg("a", "1.0.0").Save(firstPackagesFolder.Root);
                var second = new TestNupkg("a", "1.0.0").Save(secondPackagesFolder.Root);

                Func<Task> action = async () => await PushCommand.RunAsync(settings, fileSystem, new List<string>() { first.FullName, second.FullName }, false, false, log);

                await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Duplicate packages detected for 'a.1.0.0'.");
            }
        }

        [Fact]
        public async Task PushCommand_GivenInvalidNupkgVerifyLogsAndThrows()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();
                var packagePath = Path.Combine(packagesFolder.Root, "broken.nupkg");
                File.WriteAllText(packagePath, "not a package");

                Func<Task> action = async () => await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagePath }, false, false, log);

                await action.Should().ThrowAsync<Exception>();
                log.GetMessages().Should().Contain($"Invalid package '{packagePath}'.");
            }
        }

        [Fact]
        public async Task PushCommand_GivenSymbolsPackageAndSymbolsDisabledVerifySkipped()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();
                var symbols = new TestNupkg("a", "1.0.0");
                symbols.Nuspec.IsSymbolPackage = true;
                symbols.Save(packagesFolder.Root);

                await InitCommand.RunAsync(settings, fileSystem, enableCatalog: true, enableSymbols: false, log: log, token: TestContext.Current.CancellationToken);

                var success = await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);

                var packageIndex = new PackageIndex(new SleetContext()
                {
                    Token = TestContext.Current.CancellationToken,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = await FeedSettingsUtility.GetSettingsOrDefault(fileSystem, log, TestContext.Current.CancellationToken)
                });

                success.Should().BeTrue();
                (await packageIndex.GetSymbolsPackagesAsync()).Should().BeEmpty();
                log.GetMessages().Should().Contain("Skipping a 1.0.0 Symbols, to push symbols packages enable the symbols server on this feed.");
            }
        }
    }
}
