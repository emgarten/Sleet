using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class PackageDiff
    {
        /// <summary>
        /// Extra packages that should not exist.
        /// </summary>
        public HashSet<PackageIdentity> Extra { get; } = new HashSet<PackageIdentity>();

        /// <summary>
        /// Packages expected to exist that are missing.
        /// </summary>
        public HashSet<PackageIdentity> Missing { get; } = new HashSet<PackageIdentity>();

        public PackageDiff(IEnumerable<PackageIdentity> expected, IEnumerable<PackageIdentity> actual)
        {
            Extra.UnionWith(actual.Except(expected));
            Missing.UnionWith(expected.Except(actual));
        }

        public bool HasErrors
        {
            get
            {
                return Extra.Count > 0 || Missing.Count > 0;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Missing packages: {Missing.Count}");

            foreach (var package in Missing.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Version))
            {
                sb.AppendLine($"  {package.Id} {package.Version.ToFullVersionString()}");
            }

            sb.AppendLine($"Extra packages: {Extra.Count}");

            foreach (var package in Extra.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Version))
            {
                sb.AppendLine($"  {package.Id} {package.Version.ToFullVersionString()}");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
