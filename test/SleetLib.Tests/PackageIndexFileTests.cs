using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Sleet.Test;
using SleetLib.Tests;
using Xunit;

namespace Sleet.Tests
{
    public class PackageIndexFileTests
    {
        [Fact]
        public async Task PackageIndexFile_InitANewFileVerifyCreated()
        {
            using (var testContext = new SleetTestContext())
            {
                var file = new PackageIndexFile(testContext.SleetContext, "test.json", persistWhenEmpty: true);
                await file.InitAsync();
                await testContext.SleetContext.Source.Commit(testContext.SleetContext.Log, testContext.SleetContext.Token);

                var path = Path.Combine(testContext.Target, "test.json");

                File.Exists(path).Should().BeTrue();
            }
        }

        [Fact]
        public async Task PackageIndexFile_AddPackageVerifyAdd()
        {
            using (var testContext = new SleetTestContext())
            {
                var input = TestUtility.GetPackageInput("a", testContext);

                var file = new PackageIndexFile(testContext.SleetContext, "test.json", persistWhenEmpty: true);
                await file.AddPackageAsync(input);

                var packages = await file.GetPackagesAsync();
                packages.ShouldBeEquivalentTo(new[] { new PackageIdentity("a", NuGetVersion.Parse("1.0.0")) });

                var symbols = await file.GetSymbolsPackagesAsync();
                symbols.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task PackageIndexFile_RemovePackageVerifyRemove()
        {
            using (var testContext = new SleetTestContext())
            {
                var inputA = TestUtility.GetPackageInput("a", testContext);
                var inputB = TestUtility.GetPackageInput("b", testContext);

                var file = new PackageIndexFile(testContext.SleetContext, "test.json", persistWhenEmpty: true);
                await file.AddPackageAsync(inputA);
                await file.AddPackageAsync(inputB);
                await file.RemovePackageAsync(inputA.Identity);

                var packages = await file.GetPackagesAsync();
                packages.ShouldBeEquivalentTo(new[] { new PackageIdentity("b", NuGetVersion.Parse("1.0.0")) });

                var symbols = await file.GetSymbolsPackagesAsync();
                symbols.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task PackageIndexFile_RemoveAllPackagesVerifyFileRemoved()
        {
            using (var testContext = new SleetTestContext())
            {
                var inputA = TestUtility.GetPackageInput("a", testContext);
                var inputB = TestUtility.GetPackageInput("b", testContext);

                var file = new PackageIndexFile(testContext.SleetContext, "test.json", persistWhenEmpty: false);
                await file.AddPackageAsync(inputA);
                await file.AddPackageAsync(inputB);
                await file.RemovePackageAsync(inputA.Identity);
                await file.RemovePackageAsync(inputB.Identity);

                await testContext.Commit();

                var packages = await file.GetPackagesAsync();

                var path = Path.Combine(testContext.Target, "test.json");

                File.Exists(path).Should().BeFalse();
            }
        }

        [Fact]
        public async Task PackageIndexFile_RemoveAllPackagesVerifyFileNotRemoved()
        {
            using (var testContext = new SleetTestContext())
            {
                var inputA = TestUtility.GetPackageInput("a", testContext);
                var inputB = TestUtility.GetPackageInput("b", testContext);

                var file = new PackageIndexFile(testContext.SleetContext, "test.json", persistWhenEmpty: true);
                await file.AddPackageAsync(inputA);
                await file.AddPackageAsync(inputB);
                await file.RemovePackageAsync(inputA.Identity);
                await file.RemovePackageAsync(inputB.Identity);

                await testContext.Commit();

                var path = Path.Combine(testContext.Target, "test.json");

                File.Exists(path).Should().BeTrue();
            }
        }

        [Fact]
        public async Task PackageIndexFile_AddSymbolsPackageVerifyAdd()
        {
            using (var testContext = new SleetTestContext())
            {
                var input = TestUtility.GetPackageInput("a", testContext, true);

                var file = new PackageIndexFile(testContext.SleetContext, "test.json", persistWhenEmpty: true);
                await file.AddSymbolsPackageAsync(input);

                var packages = await file.GetSymbolsPackagesAsync();
                packages.ShouldBeEquivalentTo(new[] { new PackageIdentity("a", NuGetVersion.Parse("1.0.0")) });

                var nonSymbols = await file.GetPackagesAsync();
                nonSymbols.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task PackageIndexFile_RemoveSymbolsPackageVerifyRemove()
        {
            using (var testContext = new SleetTestContext())
            {
                var inputA = TestUtility.GetPackageInput("a", testContext, true);
                var inputB = TestUtility.GetPackageInput("b", testContext, true);

                var file = new PackageIndexFile(testContext.SleetContext, "test.json", persistWhenEmpty: true);
                await file.AddSymbolsPackageAsync(inputA);
                await file.AddSymbolsPackageAsync(inputB);
                await file.RemoveSymbolsPackageAsync(inputA.Identity);

                var packages = await file.GetSymbolsPackagesAsync();
                packages.ShouldBeEquivalentTo(new[] { new PackageIdentity("b", NuGetVersion.Parse("1.0.0")) });

                var nonSymbols = await file.GetPackagesAsync();
                nonSymbols.Should().BeEmpty();
            }
        }
    }
}
