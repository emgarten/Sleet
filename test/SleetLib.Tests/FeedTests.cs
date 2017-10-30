using System;
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
    public class FeedTests
    {
        [Theory]
        [InlineData("https://tempuri.org/")]
        [InlineData("https://tempuri.org")]
        [InlineData("https://tempuri.org/nuget/")]
        [InlineData("https://tempuri.org/nuget")]
        [InlineData("https://tempuri.org:8080")]
        [InlineData("https://tempuri.org:8080/")]
        [InlineData("https://tempuri.org:8080/nuget")]
        [InlineData("https://tempuri.org:8080/nuget/")]
        [InlineData("file://E:/temp")]
        [InlineData("file://E:/temp/")]
        [InlineData("file:///tmp/")]
        [InlineData("file:///tmp")]
        public async Task Feed_VerifyBaseUriIsAppliedToLocal(string baseUriString)
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();

                var fileSystemRoot = UriUtility.CreateUri(target.Root);
                var baseUri = new Uri(baseUriString);

                var fileSystem = new PhysicalFileSystem(cache, fileSystemRoot, baseUri);
                var settings = new LocalSettings();

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                    {
                        CatalogEnabled = true,
                        SymbolsEnabled = true
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

                    // Walk json to check for bad urls
#if NET45 || NETSTANDARD1_3
                    TestUtility.WalkJson(target.Root, (file, json, toCheck) =>
#else
                    await TestUtility.WalkJsonAsync(target.Root, (file, json, toCheck) =>
#endif
                    {
                        // Check only URLs found
                        if (toCheck.IndexOf("://") > -1)
                        {
                            var cleanUriSchema = toCheck.Replace(":///", string.Empty).Replace("://", string.Empty);

                            var doubleSlash = cleanUriSchema.IndexOf("//") > -1;
                            Assert.False(doubleSlash, toCheck);
                        }
                    });
                }
            }
        }
    }
}