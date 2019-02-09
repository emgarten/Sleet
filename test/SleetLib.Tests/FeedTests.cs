using System;
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
using Sleet;
using Sleet.Test.Common;
using Xunit;

namespace SleetLib.Tests
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
                    var input = PackageInput.Create(zipFile.FullName);

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
                    await TestUtility.WalkJsonAsync(target.Root, (file, json, toCheck) =>
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

        [WindowsTheory]
        [InlineData(@"c:\configPath\sleet.json", @".\", @"c:\configPath\")]
        [InlineData(@"c:\configPath\sleet.json", @".", @"c:\configPath\")]
        [InlineData(@"c:\configPath\sleet.json", @"", @"c:\configPath\")]
        [InlineData(@"c:\configPath\sleet.json", @"singleSubFolder", @"c:\configPath\singleSubFolder\")]
        [InlineData(@"c:\configPath\sleet.json", @"nestedSubFolder\a", @"c:\configPath\nestedSubFolder\a\")]
        [InlineData(@"c:\configPath\sleet.json", @"c:\absolutePath", @"c:\absolutePath\")]
        [InlineData(@"\\some-network-share\share\sleet.json", @"singleSubFolder", @"\\some-network-share\share\singleSubFolder\")]
        [InlineData(@"\\some-network-share\share\sleet.json", @"nestedSubFolder\a", @"\\some-network-share\share\nestedSubFolder\a\")]
        [InlineData(@"\\some-network-share\share\sleet.json", @".\", @"\\some-network-share\share\")]
        [InlineData(@"file:///c:/configPath/sleet.json", @"", @"c:\configPath\")]
        [InlineData(@"file:///c:/configPath/sleet.json", @"singleSubFolder", @"c:\configPath\singleSubFolder\")]
        [InlineData(@"file:///c:/configPath/sleet.json", @"nestedSubFolder\a", @"c:\configPath\nestedSubFolder\a\")]
        public void Feed_LocalTypeSupportsRelativePath(string configPath, string outputPath, string expected)
        {
            using (var cache = new LocalCache())
            {
                var baseUri = UriUtility.CreateUri("https://localhost:8080/testFeed/");

                var sleetConfig = TestUtility.CreateConfigWithLocal("local", outputPath, baseUri.AbsoluteUri);
                
                var settings = LocalSettings.Load(sleetConfig, configPath);
                var fileSystem = FileSystemFactory.CreateFileSystem(settings, cache, "local") as PhysicalFileSystem;

                Assert.NotNull(fileSystem);
                Assert.Equal(expected, fileSystem.LocalRoot);
            }
        }

        [Fact]
        public void UriUtility_ThrowsIfGetAbsolutePathWithNoSettingsFile()
        {
            Exception ex = null;

            try
            {
                using (var cache = new LocalCache())
                {
                    var baseUri = UriUtility.CreateUri("https://localhost:8080/testFeed/");

                    var sleetConfig = TestUtility.CreateConfigWithLocal("local", "relativePath", baseUri.AbsoluteUri);
                
                    var settings = LocalSettings.Load(sleetConfig, null);
                    FileSystemFactory.CreateFileSystem(settings, cache, "local");
                }
            }
            catch (Exception e)
            {
                ex = e;
            }

            ex.Should().NotBeNull();
            ex.Message.Should().Be("Cannot use a relative 'path' without a sleet.json file.");
        }
    }
}