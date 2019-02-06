using System;
using System.Collections.Generic;
using System.Text;

namespace Sleet
{
    /// <summary>
    /// Represents all entries in PackageIndexFile
    /// </summary>
    public class PackageSets
    {
        public PackageSet Packages { get; set; } = new PackageSet();

        public PackageSet Symbols { get; set; } = new PackageSet();

        public PackageSets Clone()
        {
            return new PackageSets()
            {
                Packages = Packages.Clone(),
                Symbols = Symbols.Clone()
            };
        }
    }
}
