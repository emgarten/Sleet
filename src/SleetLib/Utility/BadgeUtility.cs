using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Packaging.Core;

namespace Sleet
{
    public static class BadgeUtility
    {
        private const string COLOR_STABLE = "#007ec6";
        private const string COLOR_PRE = "#007ec6";
        private const string LABEL = "nuget";
        private const string COLOR_TOKEN = "$COLOR$";
        private const string LABEL_TOKEN = "$LABEL$";
        private const string VERSION_TOKEN = "$VERSION$";

        /// <summary>
        /// 
        /// </summary>
        public static async Task<HashSet<PackageIdentity>> UpdateBadges(SleetContext context, ISet<PackageIdentity> before, ISet<PackageIdentity> after)
        {
            var changedIds = new HashSet<string>(before.Except(after)
                .Concat(after.Except(before))
                .Select(e => e.Id),
                StringComparer.OrdinalIgnoreCase);
            


        }

        public static XDocument GetBadge(PackageIdentity package)
        {
            var color = package.Version.IsPrerelease ? COLOR_PRE : COLOR_STABLE;

            var templateString = TemplateUtility.GetBadgeTemplate();
            var s = templateString.Replace(COLOR_TOKEN, color)
                .Replace(LABEL_TOKEN, LABEL)
                .Replace(VERSION_TOKEN, package.Version.ToNormalizedString());

            return XDocument.Parse(s);
        }
    }
}
