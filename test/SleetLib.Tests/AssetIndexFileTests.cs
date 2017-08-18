using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class AssetIndexFileTests
    {
        [Fact]
        public async Task AssetIndexFile_EmptyFile()
        {
            using (var testContext = new SleetTestContext())
            {
                var identity = new PackageIdentity("a", NuGetVersion.Parse("1.0.0"));
                var file = new AssetIndexFile(testContext.SleetContext, "test.json", identity);
                await file.InitAsync();
                await testContext.SleetContext.Source.Commit(testContext.SleetContext.Log, testContext.SleetContext.Token);

                var path = Path.Combine(testContext.Target, "test.json");
                File.Exists(path).Should().BeTrue();

                var assets = await file.GetAssetsAsync();
                var symbolsAssets = await file.GetSymbolsAssetsAsync();

                assets.Should().BeEmpty();
                symbolsAssets.Should().BeEmpty();
                file.Package.Should().Be(identity);
            }
        }

        [Fact]
        public async Task AssetIndexFile_AddAssets()
        {
            using (var testContext = new SleetTestContext())
            {
                var identity = new PackageIdentity("a", NuGetVersion.Parse("1.0.0"));
                var file = new AssetIndexFile(testContext.SleetContext, "test.json", identity);

                await file.AddAssetsAsync(new[] { new AssetIndexEntry(new Uri("http://tempuri.org/a.json"), new Uri("http://tempuri.org/b.json")) });
                await file.AddSymbolsAssetsAsync(new[] { new AssetIndexEntry(new Uri("http://tempuri.org/x.json"), new Uri("http://tempuri.org/y.json")) });

                var assets = await file.GetAssetsAsync();
                var symbolsAssets = await file.GetSymbolsAssetsAsync();

                assets.Count.Should().Be(1);
                symbolsAssets.Count.Should().Be(1);

                assets.Single().PackageIndex.Should().Be(new Uri("http://tempuri.org/b.json"));
                assets.Single().Asset.Should().Be(new Uri("http://tempuri.org/a.json"));

                symbolsAssets.Single().PackageIndex.Should().Be(new Uri("http://tempuri.org/y.json"));
                symbolsAssets.Single().Asset.Should().Be(new Uri("http://tempuri.org/x.json"));
            }
        }

        [Fact]
        public async Task AssetIndexFile_RemoveAssets()
        {
            using (var testContext = new SleetTestContext())
            {
                var identity = new PackageIdentity("a", NuGetVersion.Parse("1.0.0"));
                var file = new AssetIndexFile(testContext.SleetContext, "test.json", identity);

                var asset = new AssetIndexEntry(new Uri("http://tempuri.org/a.json"), new Uri("http://tempuri.org/b.json"));
                var symAsset = new AssetIndexEntry(new Uri("http://tempuri.org/x.json"), new Uri("http://tempuri.org/y.json"));

                await file.AddAssetsAsync(new[] { asset });
                await file.AddSymbolsAssetsAsync(new[] { symAsset });

                await file.RemoveAssetsAsync(new[] { asset });

                var assets = await file.GetAssetsAsync();
                var symbolsAssets = await file.GetSymbolsAssetsAsync();

                assets.Should().BeEmpty();
                symbolsAssets.Count.Should().Be(1);
            }
        }

        [Fact]
        public async Task AssetIndexFile_RemoveSymbolsAssets()
        {
            using (var testContext = new SleetTestContext())
            {
                var identity = new PackageIdentity("a", NuGetVersion.Parse("1.0.0"));
                var file = new AssetIndexFile(testContext.SleetContext, "test.json", identity);

                var asset = new AssetIndexEntry(new Uri("http://tempuri.org/a.json"), new Uri("http://tempuri.org/b.json"));
                var symAsset = new AssetIndexEntry(new Uri("http://tempuri.org/x.json"), new Uri("http://tempuri.org/y.json"));

                await file.AddAssetsAsync(new[] { asset });
                await file.AddSymbolsAssetsAsync(new[] { symAsset });

                await file.RemoveSymbolsAssetsAsync(new[] { symAsset });

                var assets = await file.GetAssetsAsync();
                var symbolsAssets = await file.GetSymbolsAssetsAsync();

                assets.Count.Should().Be(1);
                symbolsAssets.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task AssetIndexFile_RemoveAllAssets()
        {
            using (var testContext = new SleetTestContext())
            {
                var identity = new PackageIdentity("a", NuGetVersion.Parse("1.0.0"));
                var file = new AssetIndexFile(testContext.SleetContext, "test.json", identity);

                var asset = new AssetIndexEntry(new Uri("http://tempuri.org/a.json"), new Uri("http://tempuri.org/b.json"));
                var symAsset = new AssetIndexEntry(new Uri("http://tempuri.org/x.json"), new Uri("http://tempuri.org/y.json"));

                await file.AddAssetsAsync(new[] { asset });
                await file.AddSymbolsAssetsAsync(new[] { symAsset });

                await file.RemoveAssetsAsync(new[] { asset, symAsset });
                await file.RemoveSymbolsAssetsAsync(new[] { asset, symAsset });

                var assets = await file.GetAssetsAsync();
                var symbolsAssets = await file.GetSymbolsAssetsAsync();

                assets.Should().BeEmpty();
                symbolsAssets.Should().BeEmpty();

                await testContext.SleetContext.Source.Commit(testContext.SleetContext.Log, testContext.SleetContext.Token);

                var path = Path.Combine(testContext.Target, "test.json");
                File.Exists(path).Should().BeFalse();
            }
        }
    }
}
