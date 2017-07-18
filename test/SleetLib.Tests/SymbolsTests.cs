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
                await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile.FullName }, false, false, context.Log);
                var validateOutput = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);

                Assert.True(validateOutput);
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

                var pdbExpected = Path.Combine(testContext.Target, "symbols", "SymbolsTestA.pdb", "B1680B8315F8485EA0A10F55AF08B5651", "SymbolsTestA.pdb");
                File.Exists(pdbExpected).Should().BeTrue();

                var dll2Expected = Path.Combine(testContext.Target, "symbols", "SymbolsTestB.dll", "596D8A018000", "SymbolsTestB.dll");
                File.Exists(dll2Expected).Should().BeTrue();

                var pdb2Expected = Path.Combine(testContext.Target, "symbols", "SymbolsTestB.pdb", "2C141A2023CE48F5AA68E9F5E45CDB9A1", "SymbolsTestB.pdb");
                File.Exists(pdb2Expected).Should().BeTrue();

                // Verify hashes use upper case
                var symbolsRoot = new DirectoryInfo(Path.Combine(testContext.Target, "symbols"));
                foreach (var fileNameDir in symbolsRoot.GetDirectories())
                {
                    foreach (var hashDir in fileNameDir.GetDirectories())
                    {
                        hashDir.Name.Should().Be(hashDir.Name.ToUpperInvariant());
                    }
                }
            }
        }
    }
}
