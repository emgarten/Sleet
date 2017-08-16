using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;

namespace Sleet
{
    /// <summary>
    /// Maps packages to external assets.
    /// An AssetIndexFile represents a single id/version. And both the symbols and non-symbols package types.
    /// </summary>
    public class AssetIndexFile
    {
        private readonly SleetContext _context;

        private ISleetFile File { get; set; }

        public PackageIdentity Package { get; }

        public AssetIndexFile(SleetContext context, string path, PackageIdentity package)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            Package = package ?? throw new ArgumentNullException(nameof(package));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            File = context.Source.Get(path);
        }

        public async Task AddAssetsAsync(IEnumerable<AssetIndexEntry> assets)
        {
            var assetIndex = await GetAssetIndexEntriesAsync();
            assetIndex.Packages.UnionWith(assets);
            await Save(assetIndex);
        }

        public async Task AddSymbolsAssetsAsync(IEnumerable<AssetIndexEntry> assets)
        {
            var assetIndex = await GetAssetIndexEntriesAsync();
            assetIndex.Symbols.UnionWith(assets);
            await Save(assetIndex);
        }

        public async Task<ISet<AssetIndexEntry>> GetAssetsAsync()
        {
            var assetIndex = await GetAssetIndexEntriesAsync();
            return assetIndex.Packages;
        }

        public async Task<ISet<AssetIndexEntry>> GetSymbolsAssetsAsync()
        {
            var assetIndex = await GetAssetIndexEntriesAsync();
            return assetIndex.Symbols;
        }

        public async Task RemoveAssetsAsync(IEnumerable<AssetIndexEntry> assets)
        {
            var assetIndex = await GetAssetIndexEntriesAsync();

            foreach (var asset in assets)
            {
                assetIndex.Packages.Remove(asset);
            }

            await Save(assetIndex);
        }

        public async Task RemoveSymbolsAssetsAsync(IEnumerable<AssetIndexEntry> assets)
        {
            var assetIndex = await GetAssetIndexEntriesAsync();

            foreach (var asset in assets)
            {
                assetIndex.Symbols.Remove(asset);
            }

            await Save(assetIndex);
        }

        private async Task<JObject> GetJsonAsync()
        {
            var file = File;
            return await file.GetJson(_context.Log, _context.Token);
        }

        private async Task<PackageSets> GetAssetIndexEntriesAsync()
        {
            var result = new PackageSets();

            if (await File.Exists(_context.Log, _context.Token))
            {
                var json = await GetJsonAsync();
                result.Packages = GetAssets(json, "packages");
                result.Symbols = GetAssets(json, "symbols");
            }

            return result;
        }

        private SortedSet<AssetIndexEntry> GetAssets(JObject json, string propertyName)
        {
            var assets = new SortedSet<AssetIndexEntry>();
            var packages = json[propertyName] as JArray;

            if (packages != null)
            {
                foreach (var child in packages.Select(e => (JObject)e))
                {
                    var asset = new Uri(child["asset"].Value<string>(), UriKind.Absolute);
                    var packageIndex = new Uri(child["packageIndex"].Value<string>(), UriKind.Absolute);

                    assets.Add(new AssetIndexEntry(asset, packageIndex));
                }
            }

            return assets;
        }

        private async Task Save(PackageSets index)
        {
            // Create updated index
            var json = CreateJson(index);
            var file = File;

            await file.Write(json, _context.Log, _context.Token);
        }

        private static JObject CreateJson(PackageSets index)
        {
            var json = new JObject
            {
                { "packages", CreateAssets(index.Packages) },
                { "symbols", CreateAssets(index.Symbols) }
            };

            return json;
        }

        private static JArray CreateAssets(SortedSet<AssetIndexEntry> assets)
        {
            var json = new JArray();

            foreach (var asset in assets.OrderBy(e => e.Asset.AbsoluteUri, StringComparer.Ordinal))
            {
                var child = new JObject();
                json.Add(child);

                child.Add("asset", asset.Asset.AbsoluteUri);
                child.Add("packageIndex", asset.PackageIndex.AbsoluteUri);
            }

            return json;
        }

        private class PackageSets
        {
            public SortedSet<AssetIndexEntry> Packages { get; set; } = new SortedSet<AssetIndexEntry>();

            public SortedSet<AssetIndexEntry> Symbols { get; set; } = new SortedSet<AssetIndexEntry>();
        }
    }
}
