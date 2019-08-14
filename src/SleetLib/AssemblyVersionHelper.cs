using System;
using System.Reflection;
using NuGet.Versioning;

namespace Sleet
{
    public static class AssemblyVersionHelper
    {
        /// <summary>
        /// Set this to override the assembly version.
        /// </summary>
        public static SemanticVersion VersionOverride { get; set; }

        private static SemanticVersion _version;

        /// <summary>
        /// Read the assembly version or override.
        /// </summary>
        /// <returns></returns>
        public static SemanticVersion GetVersion()
        {
            if (VersionOverride != null)
            {
                return VersionOverride;
            }

            if (_version == null)
            {
                // Read the assembly
                var assemblyVersion = typeof(AssemblyVersionHelper).GetTypeInfo().Assembly.GetName().Version;

                // Avoid going lower than 3.0.0. This can happen in some build environments and will fail tests.
                var lowestPossible = new SemanticVersion(3, 0, 0);
                var tempVersion = new SemanticVersion(Math.Max(0, assemblyVersion.Major), Math.Max(0, assemblyVersion.Minor), Math.Max(0, assemblyVersion.Build));

                if (tempVersion < lowestPossible)
                {
                    tempVersion = lowestPossible;
                }

                _version = tempVersion;
            }

            return _version;
        }
    }
}