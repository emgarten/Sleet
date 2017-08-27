using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Helpers;
using NuGet.Versioning;
using Sleet;
using Sleet.Test;
using Xunit;

namespace SleetLib.Tests
{
    public class SymbolsTests
    {
        [Fact]
        public async Task Symbols_VerifyFilesExistAfterPush()
        {
            using (var testContext = new SleetTestContext())
            {
                testContext.Root.CleanUp = false;

                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;

                var testPackage = new TestNupkg("packageA", "1.0.0");
                testPackage.Files.Clear();

                testPackage.AddFile("lib/net45/SymbolsTestA.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                testPackage.AddFile("lib/net45/SymbolsTestA.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                testPackage.AddFile("lib/net45/SymbolsTestB.dll", TestUtility.GetResource("SymbolsTestBdll").GetBytes());
                testPackage.AddFile("lib/net45/SymbolsTestB.pdb", TestUtility.GetResource("SymbolsTestBpdb").GetBytes());

                var zipFile = testPackage.Save(testContext.Packages);

                // run commands
                await InitCommand.InitAsync(context);

                // add package
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile.FullName }, false, false, context.Log);

                // validate
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                validateOutput.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Symbols_SymbolsService()
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;

                var testPackage = new TestNupkg("packageA", "1.0.0");
                testPackage.Files.Clear();

                testPackage.AddFile("lib/net45/SymbolsTestA.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                testPackage.AddFile("lib/net45/SymbolsTestA.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                testPackage.AddFile("lib/net45/SymbolsTestB.dll", TestUtility.GetResource("SymbolsTestBdll").GetBytes());
                testPackage.AddFile("lib/net45/SymbolsTestB.pdb", TestUtility.GetResource("SymbolsTestBpdb").GetBytes());

                var zipFile = testPackage.Save(testContext.Packages);
                var packageInput = testContext.GetPackageInput(zipFile);

                var symbolsService = new Symbols(context);
                await symbolsService.AddPackageAsync(packageInput);

                await context.Source.Commit(context.Log, CancellationToken.None);

                var dllExpected = Path.Combine(testContext.Target, "symbols", "SymbolsTestA.dll", "A7F83EF08000", "SymbolsTestA.dll");
                File.Exists(dllExpected).Should().BeTrue();

                var pdbExpected = Path.Combine(testContext.Target, "symbols", "SymbolsTestA.pdb", "B1680B8315F8485EA0A10F55AF08B565ffffffff", "SymbolsTestA.pdb");
                File.Exists(pdbExpected).Should().BeTrue();

                var dll2Expected = Path.Combine(testContext.Target, "symbols", "SymbolsTestB.dll", "596D8A018000", "SymbolsTestB.dll");
                File.Exists(dll2Expected).Should().BeTrue();

                var pdb2Expected = Path.Combine(testContext.Target, "symbols", "SymbolsTestB.pdb", "2C141A2023CE48F5AA68E9F5E45CDB9A1", "SymbolsTestB.pdb");
                File.Exists(pdb2Expected).Should().BeTrue();
            }
        }

        [Fact]
        public async Task Symbols_SymbolsServiceDuplicateFileNames()
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;

                var testPackage = new TestNupkg("packageA", "1.0.0");
                testPackage.Files.Clear();

                testPackage.AddFile("lib/net45/SymbolsTest.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                testPackage.AddFile("lib/net45/SymbolsTest.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                testPackage.AddFile("lib/netstandard1.3/SymbolsTest.dll", TestUtility.GetResource("SymbolsTestBdll").GetBytes());
                testPackage.AddFile("lib/netstandard1.3/SymbolsTest.pdb", TestUtility.GetResource("SymbolsTestBpdb").GetBytes());

                var zipFile = testPackage.Save(testContext.Packages);
                var packageInput = testContext.GetPackageInput(zipFile);

                var symbolsService = new Symbols(context);
                await symbolsService.AddPackageAsync(packageInput);

                await context.Source.Commit(context.Log, CancellationToken.None);

                var dllExpected = Path.Combine(testContext.Target, "symbols", "SymbolsTest.dll", "A7F83EF08000", "SymbolsTest.dll");
                File.Exists(dllExpected).Should().BeTrue();

                var pdbExpected = Path.Combine(testContext.Target, "symbols", "SymbolsTest.pdb", "B1680B8315F8485EA0A10F55AF08B565ffffffff", "SymbolsTest.pdb");
                File.Exists(pdbExpected).Should().BeTrue();

                var dll2Expected = Path.Combine(testContext.Target, "symbols", "SymbolsTest.dll", "596D8A018000", "SymbolsTest.dll");
                File.Exists(dll2Expected).Should().BeTrue();

                var pdb2Expected = Path.Combine(testContext.Target, "symbols", "SymbolsTest.pdb", "2C141A2023CE48F5AA68E9F5E45CDB9A1", "SymbolsTest.pdb");
                File.Exists(pdb2Expected).Should().BeTrue();
            }
        }

        [Fact]
        public async Task Symbols_AddPackageAndValidateVerifyNoFailures()
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;

                var testPackage = new TestNupkg("a", "1.0.0");
                testPackage.Files.Clear();

                testPackage.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                testPackage.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());

                var zipFile = testPackage.Save(testContext.Packages);
                var packageInput = testContext.GetPackageInput(zipFile);

                var service = new Symbols(context);
                await service.AddPackageAsync(packageInput);

                // Validate
                var messages = await service.ValidateAsync();
                var hasErrors = messages.Any(e => e.Level == LogLevel.Error);

                hasErrors.Should().BeFalse();
            }
        }

        [Fact]
        public async Task Symbols_AddSymbolsPackageAndValidateVerifyNoFailures()
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;

                var testPackage = new TestNupkg("a", "1.0.0");
                testPackage.Files.Clear();
                testPackage.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                testPackage.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                testPackage.Nuspec.IsSymbolPackage = true;

                var zipFile = testPackage.Save(testContext.Packages);
                var packageInput = testContext.GetPackageInput(zipFile);

                var service = new Symbols(context);
                await service.AddSymbolsPackageAsync(packageInput);

                // Validate
                var messages = await service.ValidateAsync();
                var hasErrors = messages.Any(e => e.Level == LogLevel.Error);

                hasErrors.Should().BeFalse();
            }
        }

        [Fact]
        public async Task Symbols_AddPackageAndSymbolsPackageAndValidateVerifyNoFailures()
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;

                var pkgA = new TestNupkg("a", "1.0.0");
                pkgA.Files.Clear();
                pkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());

                var symPkgA = new TestNupkg("a", "1.0.0");
                symPkgA.Files.Clear();
                symPkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                symPkgA.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                symPkgA.Nuspec.IsSymbolPackage = true;

                var pkgAZip = pkgA.Save(testContext.Packages);
                var pkgAInput = testContext.GetPackageInput(pkgAZip);

                var symPkgAZip = symPkgA.Save(testContext.Packages);
                var symPkgAInput = testContext.GetPackageInput(symPkgAZip);

                var service = new Symbols(context);
                await service.AddPackageAsync(pkgAInput);
                await service.AddSymbolsPackageAsync(symPkgAInput);

                // Validate
                var messages = await service.ValidateAsync();
                var hasErrors = messages.Any(e => e.Level == LogLevel.Error);

                hasErrors.Should().BeFalse();
            }
        }

        [Fact]
        public async Task Symbols_ValidationVerifyMissingIndexCausesFailure()
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;
                var service = new Symbols(context);

                // Add packages
                await AddBasicPackagesAsync(testContext, service);

                // Corrupt feed
                service.PackageIndex.File.Delete(context.Log, context.Token);

                // Validate
                var messages = await service.ValidateAsync();
                var hasErrors = messages.Any(e => e.Level == LogLevel.Error);

                hasErrors.Should().BeTrue();
            }
        }

        // Add package to index which is not in the main package index, verify validation failure
        [Fact]
        public async Task Symbols_ValidationVerifyExtraPackageInIndexCausesFailure()
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;
                var service = new Symbols(context);

                // Corrupt feed
                var index = new PackageIndex(context);

                var pkgA = new TestNupkg("a", "1.0.0");
                pkgA.Files.Clear();
                pkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                var pkgAZip = pkgA.Save(testContext.Packages);
                var pkgAInput = testContext.GetPackageInput(pkgAZip);

                await index.AddPackageAsync(pkgAInput);

                // Validate
                var messages = await service.ValidateAsync();
                var hasErrors = messages.Any(e => e.Level == LogLevel.Error);

                hasErrors.Should().BeTrue();
            }
        }

        // Add symbols package to index which is not in the main package index, verify validation failure
        [Fact]
        public async Task Symbols_ValidationVerifyExtraSymbolsPackageInIndexCausesFailure()
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;
                var service = new Symbols(context);

                // Corrupt feed
                var index = new PackageIndex(context);

                var pkgA = new TestNupkg("a", "1.0.0");
                pkgA.Files.Clear();
                pkgA.Nuspec.IsSymbolPackage = true;
                pkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                var pkgAZip = pkgA.Save(testContext.Packages);
                var pkgAInput = testContext.GetPackageInput(pkgAZip);

                await index.AddSymbolsPackageAsync(pkgAInput);

                // Validate
                var messages = await service.ValidateAsync();
                var hasErrors = messages.Any(e => e.Level == LogLevel.Error);

                hasErrors.Should().BeTrue();
            }
        }

        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public async Task Symbols_ValidationVerifyMissingAssemblyIndexCausesFailure(string isSymbolsString)
        {
            var isSymbols = bool.Parse(isSymbolsString);

            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;
                var service = new Symbols(context);

                // Add packages
                await AddPackageAsync(isSymbols, testContext, service);

                // Corrupt feed
                var path = SymbolsIndexUtility.GetAssemblyToPackageIndexPath("a.dll", "A7F83EF08000");
                var assemblyPackageIndex = new AssetIndexFile(testContext.SleetContext, path, new PackageIdentity("a", NuGetVersion.Parse("1.0.0")));
                var exists = await assemblyPackageIndex.File.Exists(context.Log, context.Token);
                exists.Should().BeTrue();
                assemblyPackageIndex.File.Delete(context.Log, context.Token);

                // Validate
                var messages = await service.ValidateAsync();
                var hasErrors = messages.Any(e => e.Level == LogLevel.Error);

                hasErrors.Should().BeTrue();
            }
        }

        // Add packages, remove asset package index link, verify validation failure
        // Add packages, remove asset symbols package index link, verify validation failure
        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public async Task Symbols_ValidationVerifyMissingAssemblyIndexEntryCausesFailure(string isSymbolsString)
        {
            var isSymbols = bool.Parse(isSymbolsString);

            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;
                var service = new Symbols(context);

                // Add packages
                await AddPackageAsync(isSymbols, testContext, service);

                // Corrupt feed
                var path = SymbolsIndexUtility.GetAssemblyToPackageIndexPath("a.dll", "A7F83EF08000");
                var assemblyPackageIndex = new AssetIndexFile(testContext.SleetContext, path, new PackageIdentity("a", NuGetVersion.Parse("1.0.0")));
                var exists = await assemblyPackageIndex.File.Exists(context.Log, context.Token);
                exists.Should().BeTrue();

                // Swap package <-> symbols to corrupt
                if (isSymbols)
                {
                    await assemblyPackageIndex.AddAssetsAsync(await assemblyPackageIndex.GetSymbolsAssetsAsync());
                }
                else
                {
                    await assemblyPackageIndex.AddSymbolsAssetsAsync(await assemblyPackageIndex.GetAssetsAsync());
                }

                // Validate
                var messages = await service.ValidateAsync();
                var hasErrors = messages.Any(e => e.Level == LogLevel.Error);

                hasErrors.Should().BeTrue();
            }
        }

        // Add package with no assemblies, verify not added to symbols index
        // Add symbols package with no assemblies, verify not added to symbols index
        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public async Task Symbols_AddPackageWithSymbolsVerifyInIndex(string isSymbolsString)
        {
            var isSymbols = bool.Parse(isSymbolsString);

            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;
                var symbols = new Symbols(context);
                var packageIndex = new PackageIndex(context);

                // Create package
                var pkgA = new TestNupkg("a", "1.0.0");
                pkgA.Files.Clear();
                pkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                pkgA.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                pkgA.Nuspec.IsSymbolPackage = isSymbols;
                var zip = pkgA.Save(testContext.Packages);
                var pkgInput = testContext.GetPackageInput(zip);

                // Init
                var success = await InitCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.SleetContext.Log,
                    token: CancellationToken.None);

                // Push
                success &= await PushCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    new List<string>() { zip.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.SleetContext.Log);

                // Validate
                success &= await ValidateCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    testContext.SleetContext.Log);

                var symbolsIndex = new HashSet<PackageIdentity>();
                var packageIndexPkgs = new HashSet<PackageIdentity>();

                if (isSymbols)
                {
                    symbolsIndex.UnionWith(await symbols.GetSymbolsPackagesAsync());
                    packageIndexPkgs.UnionWith(await packageIndex.GetSymbolsPackagesAsync());
                }
                else
                {
                    symbolsIndex.UnionWith(await symbols.GetPackagesAsync());
                    packageIndexPkgs.UnionWith(await packageIndex.GetPackagesAsync());
                }

                // Verify package does not show up in symbols index
                symbolsIndex.Should().BeEquivalentTo(new[] { new PackageIdentity("a", NuGetVersion.Parse("1.0.0")) });
                packageIndexPkgs.Should().BeEquivalentTo(new[] { new PackageIdentity("a", NuGetVersion.Parse("1.0.0")) });

                // Validate
                success.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Symbols_AddSymbolsPackageVerifyFeed()
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;
                var symbols = new Symbols(context);
                var packageIndex = new PackageIndex(context);
                var catalog = new Catalog(context);
                var autoComplete = new AutoComplete(context);
                var flatContainer = new FlatContainer(context);
                var registrations = new Registrations(context);
                var search = new Search(context);

                // Create package
                var pkgA = new TestNupkg("a", "1.0.0");
                pkgA.Files.Clear();
                pkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                pkgA.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                pkgA.Nuspec.IsSymbolPackage = true;
                var zip = pkgA.Save(testContext.Packages);
                var pkgInput = testContext.GetPackageInput(zip);

                // Init
                var success = await InitCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.SleetContext.Log,
                    token: CancellationToken.None);

                // Push
                success &= await PushCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    new List<string>() { zip.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.SleetContext.Log);

                // Validate
                success &= await ValidateCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    testContext.SleetContext.Log);

                success.Should().BeTrue();

                // Exists under symbols
                (await symbols.GetSymbolsPackagesAsync()).Should().NotBeEmpty();
                (await packageIndex.GetSymbolsPackagesAsync()).Should().NotBeEmpty();

                // Does not exist in non-symbols
                (await symbols.GetPackagesAsync()).Should().BeEmpty();
                (await packageIndex.GetPackagesAsync()).Should().BeEmpty();

                // Verify it does not appear in other services
                (await catalog.GetPackagesAsync()).Should().BeEmpty();
                (await autoComplete.GetPackageIds()).Should().BeEmpty();
                (await flatContainer.GetPackagesByIdAsync("a")).Should().BeEmpty();
                (await registrations.GetPackagesByIdAsync("a")).Should().BeEmpty();
                (await search.GetPackagesAsync()).Should().BeEmpty();

                // Verify nupkg exists
                var nupkgPath = Path.Combine(testContext.Target, "symbols", "packages", "a", "1.0.0", "a.1.0.0.symbols.nupkg");
                File.Exists(nupkgPath).Should().BeTrue();

                // Verify package details
                var detailsPath = Path.Combine(testContext.Target, "symbols", "packages", "a", "1.0.0", "package.json");
                File.Exists(detailsPath).Should().BeTrue();
            }
        }

        [Fact]
        public async Task Symbols_AddSymbolsPackageWithNoValidSymbolsVerifyFeed()
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;
                var symbols = new Symbols(context);
                var packageIndex = new PackageIndex(context);
                var catalog = new Catalog(context);
                var autoComplete = new AutoComplete(context);
                var flatContainer = new FlatContainer(context);
                var registrations = new Registrations(context);
                var search = new Search(context);

                // Create package
                var pkgA = new TestNupkg("a", "1.0.0");
                pkgA.Nuspec.IsSymbolPackage = true;
                var zip = pkgA.Save(testContext.Packages);
                var pkgInput = testContext.GetPackageInput(zip);

                // Init
                var success = await InitCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.SleetContext.Log,
                    token: CancellationToken.None);

                // Push
                success &= await PushCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    new List<string>() { zip.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.SleetContext.Log);

                // Validate
                success &= await ValidateCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    testContext.SleetContext.Log);

                success.Should().BeTrue();

                // The nupkg should exist, but there should not be any assets added.
                (await symbols.GetSymbolsPackagesAsync()).Should().BeEmpty();
                (await packageIndex.GetSymbolsPackagesAsync()).Should().NotBeEmpty();

                // Verify nupkg exists
                var nupkgPath = Path.Combine(testContext.Target, "symbols", "packages", "a", "1.0.0", "a.1.0.0.symbols.nupkg");
                File.Exists(nupkgPath).Should().BeTrue();

                // Verify package details
                var detailsPath = Path.Combine(testContext.Target, "symbols", "packages", "a", "1.0.0", "package.json");
                File.Exists(detailsPath).Should().BeTrue();
            }
        }

        [Fact]
        public async Task Symbols_AddSymbolsPackageWithSymbolsOffVerifySkipped()
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;

                // Create package
                var pkgA = new TestNupkg("a", "1.0.0");
                pkgA.Files.Clear();
                pkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                pkgA.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                pkgA.Nuspec.IsSymbolPackage = true;
                var zip = pkgA.Save(testContext.Packages);
                var pkgInput = testContext.GetPackageInput(zip);

                // Init
                var success = await InitCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    enableCatalog: true,
                    enableSymbols: false,
                    log: testContext.SleetContext.Log,
                    token: CancellationToken.None);

                // Push
                success &= await PushCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    new List<string>() { zip.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.SleetContext.Log);

                success.Should().BeTrue();

                var packageIndex = new PackageIndex(context);
                (await packageIndex.IsEmpty()).Should().BeTrue();

                var testLogger = (TestLogger)testContext.SleetContext.Log;
                testLogger.GetMessages().Should().Contain("to push symbols package enable the symbols server on this feed");
            }
        }

        // Add package with no assemblies, verify not added to symbols index
        // Add symbols package with no assemblies, verify not added to symbols index
        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public async Task Symbols_AddPackageWithNoSymbolsVerifyNotInIndex(string isSymbolsString)
        {
            var isSymbols = bool.Parse(isSymbolsString);

            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;

                // Create package
                var pkgA = new TestNupkg("a", "1.0.0");
                pkgA.Files.Clear();
                pkgA.Nuspec.IsSymbolPackage = isSymbols;
                var zip = pkgA.Save(testContext.Packages);
                var pkgInput = testContext.GetPackageInput(zip);

                // Init
                var success = await InitCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.SleetContext.Log,
                    token: CancellationToken.None);

                // Push
                success &= await PushCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    new List<string>() { zip.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.SleetContext.Log);

                // Validate
                success &= await ValidateCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    testContext.SleetContext.Log);

                var service = new Symbols(context);
                var packages = new HashSet<PackageIdentity>(await service.GetPackagesAsync());
                packages.UnionWith(await service.GetSymbolsPackagesAsync());

                // Verify package does not show up in symbols index
                packages.Should().BeEmpty();

                // Validate
                success.Should().BeTrue();
            }
        }

        // Add and remove a symbols package, verify all files are gone
        [Fact]
        public async Task Symbols_RemovePackageVerifySymbolsRemoved()
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;

                // Create package
                var identity = new PackageIdentity("a", NuGetVersion.Parse("1.0.0"));
                var pkgA = new TestNupkg("a", "1.0.0");
                pkgA.Files.Clear();
                pkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                pkgA.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                pkgA.Nuspec.IsSymbolPackage = true;
                var zip = pkgA.Save(testContext.Packages);
                var pkgInput = testContext.GetPackageInput(zip);

                // File path
                var nupkgPath = Path.Combine(testContext.Target, ToLocalPath(SymbolsIndexUtility.GetSymbolsNupkgPath(identity)));
                var detailsPath = Path.Combine(testContext.Target, ToLocalPath(SymbolsIndexUtility.GetSymbolsPackageDetailsPath(identity)));
                var dllPath = Path.Combine(testContext.Target, "symbols", "a.dll", "A7F83EF08000", "a.dll");
                var dllPackagesJsonPath = Path.Combine(testContext.Target, "symbols", "a.dll", "A7F83EF08000", "packages.json");
                var pdbPath = Path.Combine(testContext.Target, "symbols", "a.pdb", "B1680B8315F8485EA0A10F55AF08B565ffffffff", "a.pdb");
                var pdbPackagesJsonPath = Path.Combine(testContext.Target, "symbols", "a.pdb", "B1680B8315F8485EA0A10F55AF08B565ffffffff", "packages.json");
                var packageAssetsPath = Path.Combine(testContext.Target, ToLocalPath(SymbolsIndexUtility.GetPackageToAssemblyIndexPath(identity)));

                var files = new List<string>
                {
                    nupkgPath,
                    detailsPath,
                    dllPath,
                    dllPackagesJsonPath,
                    pdbPath,
                    pdbPackagesJsonPath,
                    packageAssetsPath
                };

                // Init
                var success = await InitCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.SleetContext.Log,
                    token: CancellationToken.None);

                // Push
                success &= await PushCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    new List<string>() { zip.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.SleetContext.Log);

                // Verify files are present
                files.ForEach(e => File.Exists(e).Should().BeTrue(e));

                // Remove
                success &= await DeleteCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    "a",
                    "1.0.0",
                    "reason",
                    force: false,
                    log: testContext.SleetContext.Log);

                // Verify files are gone
                files.ForEach(e => File.Exists(e).Should().BeFalse(e));

                // Validate
                success &= await ValidateCommand.RunAsync(
                        testContext.SleetContext.LocalSettings,
                        testContext.SleetContext.Source,
                        log: testContext.SleetContext.Log);

                success.Should().BeTrue();

                var testLogger = (TestLogger)testContext.SleetContext.Log;
                testLogger.GetMessages().Should().Contain("Removing symbols package a.1.0.0");
            }
        }

        [Fact]
        public async Task Symbols_AddSymbolsPackagesContainingTheSameFilesVerifyDeleteDoesNotRemove()
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;

                // Create package
                var identity = new PackageIdentity("a", NuGetVersion.Parse("1.0.0"));
                var pkgA = new TestNupkg("a", "1.0.0");
                pkgA.Files.Clear();
                pkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                pkgA.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                pkgA.Nuspec.IsSymbolPackage = true;
                var zip = pkgA.Save(testContext.Packages);
                var pkgInput = testContext.GetPackageInput(zip);

                var identityB = new PackageIdentity("b", NuGetVersion.Parse("1.0.0"));
                var pkgB = new TestNupkg("b", "1.0.0");
                pkgB.Files.Clear();
                pkgB.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                pkgB.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                pkgB.Nuspec.IsSymbolPackage = true;
                var zipB = pkgB.Save(testContext.Packages);
                var pkgInputB = testContext.GetPackageInput(zipB);

                // File path
                var dllPath = Path.Combine(testContext.Target, "symbols", "a.dll", "A7F83EF08000", "a.dll");
                var dllPackagesJsonPath = Path.Combine(testContext.Target, "symbols", "a.dll", "A7F83EF08000", "packages.json");
                var pdbPath = Path.Combine(testContext.Target, "symbols", "a.pdb", "B1680B8315F8485EA0A10F55AF08B565ffffffff", "a.pdb");
                var pdbPackagesJsonPath = Path.Combine(testContext.Target, "symbols", "a.pdb", "B1680B8315F8485EA0A10F55AF08B565ffffffff", "packages.json");

                var files = new List<string>
                {
                    dllPath,
                    dllPackagesJsonPath,
                    pdbPath,
                    pdbPackagesJsonPath
                };

                // Init
                var success = await InitCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.SleetContext.Log,
                    token: CancellationToken.None);

                // Push
                success &= await PushCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    new List<string>() { zip.FullName, zipB.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.SleetContext.Log);

                // Verify files are present
                files.ForEach(e => File.Exists(e).Should().BeTrue(e));

                // Remove
                success &= await DeleteCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    "a",
                    "1.0.0",
                    "reason",
                    force: false,
                    log: testContext.SleetContext.Log);

                // Verify files are still present
                files.ForEach(e => File.Exists(e).Should().BeTrue(e));

                // Validate
                success &= await ValidateCommand.RunAsync(
                        testContext.SleetContext.LocalSettings,
                        testContext.SleetContext.Source,
                        log: testContext.SleetContext.Log);

                success.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Symbols_AddAndRemovePackagesMultipleTimesVerifyValidation()
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;

                // Create package
                var identity = new PackageIdentity("a", NuGetVersion.Parse("1.0.0"));
                var pkgA = new TestNupkg("a", "1.0.0");
                pkgA.Files.Clear();
                pkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                pkgA.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                var zip = pkgA.Save(testContext.Packages);
                var pkgInput = testContext.GetPackageInput(zip);

                var symPkgA = new TestNupkg("a", "1.0.0");
                symPkgA.Files.Clear();
                symPkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                symPkgA.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                symPkgA.Nuspec.IsSymbolPackage = true;
                var symZip = symPkgA.Save(testContext.Packages);
                var symPkgInput = testContext.GetPackageInput(symZip);

                // Init
                var success = await InitCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.SleetContext.Log,
                    token: CancellationToken.None);

                // Push
                success &= await PushCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    new List<string>() { zip.FullName, symZip.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.SleetContext.Log);

                // Validate 1
                success &= await ValidateCommand.RunAsync(
                        testContext.SleetContext.LocalSettings,
                        testContext.SleetContext.Source,
                        log: testContext.SleetContext.Log);

                // Remove
                success &= await DeleteCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    "a",
                    "1.0.0",
                    "reason",
                    force: false,
                    log: testContext.SleetContext.Log);

                // Validate 2
                success &= await ValidateCommand.RunAsync(
                        testContext.SleetContext.LocalSettings,
                        testContext.SleetContext.Source,
                        log: testContext.SleetContext.Log);

                // Push Again
                success &= await PushCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    new List<string>() { zip.FullName, symZip.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.SleetContext.Log);

                // Validate 3
                success &= await ValidateCommand.RunAsync(
                        testContext.SleetContext.LocalSettings,
                        testContext.SleetContext.Source,
                        log: testContext.SleetContext.Log);

                // Remove Again
                success &= await DeleteCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    "a",
                    "1.0.0",
                    "reason",
                    force: false,
                    log: testContext.SleetContext.Log);

                // Validate 4
                success &= await ValidateCommand.RunAsync(
                        testContext.SleetContext.LocalSettings,
                        testContext.SleetContext.Source,
                        log: testContext.SleetContext.Log);

                success.Should().BeTrue();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Symbols_ForcePushPackageShouldNotAffectOtherType(bool isSymbols)
        {
            using (var testContext = new SleetTestContext())
            {
                var context = testContext.SleetContext;
                context.SourceSettings.SymbolsEnabled = true;

                // Create package
                var identity = new PackageIdentity("a", NuGetVersion.Parse("1.0.0"));
                var pkgA = new TestNupkg("a", "1.0.0");
                pkgA.Files.Clear();
                pkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                pkgA.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                var zip = pkgA.Save(testContext.Packages);
                var pkgInput = testContext.GetPackageInput(zip);

                var symPkgA = new TestNupkg("a", "1.0.0");
                symPkgA.Files.Clear();
                symPkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
                symPkgA.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
                symPkgA.Nuspec.IsSymbolPackage = true;
                var symZip = symPkgA.Save(testContext.Packages);
                var symPkgInput = testContext.GetPackageInput(symZip);

                var forcePushZip = zip.FullName;

                if (isSymbols)
                {
                    forcePushZip = symZip.FullName;
                }

                // Init
                var success = await InitCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.SleetContext.Log,
                    token: CancellationToken.None);

                // Push
                success &= await PushCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    new List<string>() { zip.FullName, symZip.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.SleetContext.Log);

                // Force push
                success &= await PushCommand.RunAsync(
                    testContext.SleetContext.LocalSettings,
                    testContext.SleetContext.Source,
                    new List<string>() { forcePushZip },
                    force: true,
                    skipExisting: false,
                    log: testContext.SleetContext.Log);

                // Validate
                success &= await ValidateCommand.RunAsync(
                        testContext.SleetContext.LocalSettings,
                        testContext.SleetContext.Source,
                        log: testContext.SleetContext.Log);

                success.Should().BeTrue();

                // Both packages should exist, force should not delete one or the other.
                var index = new PackageIndex(context);
                (await index.Exists(identity)).Should().BeTrue();
                (await index.SymbolsExists(identity)).Should().BeTrue();
            }
        }

        private static async Task AddPackageAsync(bool isSymbols, SleetTestContext testContext, Symbols service)
        {
            var pkgA = new TestNupkg("a", "1.0.0");
            pkgA.Files.Clear();
            pkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
            pkgA.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
            pkgA.Nuspec.IsSymbolPackage = isSymbols;
            var zip = pkgA.Save(testContext.Packages);
            var pkgInput = testContext.GetPackageInput(zip);

            if (isSymbols)
            {
                await service.AddSymbolsPackageAsync(pkgInput);
            }
            else
            {
                await service.AddPackageAsync(pkgInput);
            }
        }

        // Add a.1.0.0.nupkg and a.symbols.1.0.0.nupkg
        private static async Task AddBasicPackagesAsync(SleetTestContext testContext, Symbols service)
        {
            var pkgA = new TestNupkg("a", "1.0.0");
            pkgA.Files.Clear();
            pkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());

            var symPkgA = new TestNupkg("a", "1.0.0");
            symPkgA.Files.Clear();
            symPkgA.AddFile("lib/net45/a.dll", TestUtility.GetResource("SymbolsTestAdll").GetBytes());
            symPkgA.AddFile("lib/net45/a.pdb", TestUtility.GetResource("SymbolsTestApdb").GetBytes());
            symPkgA.Nuspec.IsSymbolPackage = true;

            var pkgAZip = pkgA.Save(testContext.Packages);
            var pkgAInput = testContext.GetPackageInput(pkgAZip);

            var symPkgAZip = symPkgA.Save(testContext.Packages);
            var symPkgAInput = testContext.GetPackageInput(symPkgAZip);


            await service.AddPackageAsync(pkgAInput);
            await service.AddSymbolsPackageAsync(symPkgAInput);
        }

        private static string ToLocalPath(string s)
        {
            return s.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
