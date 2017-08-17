using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
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
    }
}
