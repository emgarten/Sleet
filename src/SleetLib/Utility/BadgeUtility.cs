using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Sleet
{
    public static class BadgeUtility
    {
        private const string COLOR_STABLE = "#007ec6";
        private const string COLOR_PRE = "#dfb317";
        private const string LABEL = "nuget";
        private const string COLOR_TOKEN = "$COLOR$";
        private const string LABEL_TOKEN = "$LABEL$";
        private const string VERSION_TOKEN = "$VERSION$";

        /// <summary>
        /// Update all feed badges
        /// </summary>
        public static async Task UpdateBadges(SleetContext context, ISet<PackageIdentity> before, ISet<PackageIdentity> after)
        {
            var stable = GetChanges(before, after, preRel: false);
            var pre = GetChanges(before, after, preRel: true);

            await UpdateBadges(context, stable, preRel: false);
            await UpdateBadges(context, pre, preRel: true);
        }

        /// <summary>
        /// Update feed badges for stable or prerelease
        /// </summary>
        public static async Task UpdateBadges(SleetContext context, ISet<PackageIdentity> updates, bool preRel)
        {
            foreach (var package in updates)
            {
                await UpdateOrRemoveBadge(context, package, preRel);
            }
        }

        /// <summary>
        /// Write or remove badge file from the feed.
        /// </summary>
        public static async Task UpdateOrRemoveBadge(SleetContext context, PackageIdentity package, bool preRel)
        {
            var svgPath = $"badges/{(preRel ? "vpre" : "v")}/{package.Id}.svg".ToLowerInvariant();
            var jsonPath = $"badges/{(preRel ? "vpre" : "v")}/{package.Id}.json".ToLowerInvariant();

            var svgFile = context.Source.Get(svgPath);
            var jsonFile = context.Source.Get(jsonPath);

            // If the identity doesn't have it version then it should be removed
            if (package.HasVersion)
            {
                using (var stream = new MemoryStream())
                {
                    GetSvgBadge(package, preRel).Save(stream);
                    stream.Position = 0;

                    await svgFile.Write(stream, context.Log, context.Token);
                }

                await jsonFile.Write(GetJsonBadge(package, preRel), context.Log, context.Token);
            }
            else
            {
                svgFile.Delete(context.Log, context.Token);
                jsonFile.Delete(context.Log, context.Token);
            }
        }

        /// <summary>
        /// Find all packages that should be updated. If a package is removed the identity version will be null.
        /// </summary>
        public static ISet<PackageIdentity> GetChanges(ISet<PackageIdentity> before, ISet<PackageIdentity> after, bool preRel)
        {
            var toUpdate = new SortedSet<PackageIdentity>();

            var changedIds = new SortedSet<string>(before.Except(after)
                .Concat(after.Except(before))
                .Select(e => e.Id),
                StringComparer.OrdinalIgnoreCase);

            foreach (var id in changedIds)
            {
                // Either version could be null
                var maxAfter = GetMaxVersion(after, id, preRel);
                var maxBefore = GetMaxVersion(before, id, preRel);

                if (maxAfter != maxBefore)
                {
                    // Identity could have a null version if the package id was removed
                    toUpdate.Add(new PackageIdentity(id, maxAfter));
                }
            }

            return toUpdate;
        }

        public static NuGetVersion GetMaxVersion(ISet<PackageIdentity> packages, string id, bool includePre)
        {
            var versions = new SortedSet<NuGetVersion>(packages.Where(e => StringComparer.OrdinalIgnoreCase.Equals(id, e.Id)).Select(e => e.Version));

            // Find the max with respect to stable vs prerel
            var max = versions.Where(e => (includePre || !e.IsPrerelease)).Max();

            // If no stable versions exist, take the max prerel
            if (max == null)
            {
                max = versions.Max();
            }

            return max;
        }

        public static XDocument GetSvgBadge(PackageIdentity package, bool includePre)
        {
            var color = includePre ? COLOR_PRE : COLOR_STABLE;

            var templateString = TemplateUtility.GetBadgeTemplate();
            var s = templateString.Replace(COLOR_TOKEN, color)
                .Replace(LABEL_TOKEN, LABEL)
                .Replace(VERSION_TOKEN, package.Version.ToNormalizedString());

            return XDocument.Parse(s);
        }

        public static JObject GetJsonBadge(PackageIdentity package, bool includePre)
        {
            var color = includePre ? COLOR_PRE : COLOR_STABLE;

            var json = new JObject();
            json.Add("schemaVersion", 1);
            json.Add("label", LABEL);
            json.Add("message", package.Version.ToNormalizedString());
            json.Add("color", color);
            return json;
        }
    }
}
