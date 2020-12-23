using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        /// <summary>
        /// Update all feed badges
        /// </summary>
        public static Task UpdateBadges(SleetContext context, ISet<PackageIdentity> before, ISet<PackageIdentity> after)
        {
            var stable = GetChanges(before, after, preRel: false);
            var pre = GetChanges(before, after, preRel: true);

            return Task.WhenAll(UpdateBadges(context, stable, preRel: false), UpdateBadges(context, pre, preRel: true));
        }

        /// <summary>
        /// Update feed badges for stable or prerelease
        /// </summary>
        public static Task UpdateBadges(SleetContext context, ISet<PackageIdentity> updates, bool preRel)
        {
            var tasks = new List<Func<Task>>(updates.Select(e => new Func<Task>(() => UpdateOrRemoveBadge(context, e, preRel))));
            return TaskUtils.RunAsync(tasks);
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
                await jsonFile.Write(GetJsonBadge(package), context.Log, context.Token);
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

        public static JObject GetJsonBadge(PackageIdentity package)
        {
            var color = package.Version.IsPrerelease ? COLOR_PRE : COLOR_STABLE;

            var json = new JObject
            {
                { "schemaVersion", 1 },
                { "label", LABEL },
                { "message", GetBadgeVersion(package.Version) },
                { "color", color }
            };
            return json;
        }

        private static string GetBadgeVersion(NuGetVersion version)
        {
            return $"v{version.ToNormalizedString()}";
        }
    }
}
