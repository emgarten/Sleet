using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
                    Assert.Equal(1, catalogEntries.Count);
                    Assert.Equal(1, indexPackages.Count);
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
                Assert.Equal(1, indexPackages.Count);
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
                Assert.Equal(1, indexPackages.Count);
            }
        }
    }
}
