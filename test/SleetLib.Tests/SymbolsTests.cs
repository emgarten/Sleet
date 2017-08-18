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

        // Add package with no assemblies, verify not added to symbols index
        // Add symbols package with no assemblies, verify not added to symbols index
        // Add packages, remove package, verify validation
        // Add packages, remove symbols package, verify validation
    }
}
