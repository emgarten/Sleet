using System;
using System.Reflection;
using NuGet.Versioning;

namespace Sleet
{
    public static class AssemblyVersionHelper
    {
        private static volatile SemanticVersion _versionOverride;
        private static volatile SemanticVersion _version;
        private static readonly object _lock = new object();

        /// <summary>
        /// Set this to override the assembly version.
        /// </summary>
        public static SemanticVersion VersionOverride 
        { 
            get => _versionOverride;
            set => _versionOverride = value;
        }

        /// <summary>
        /// Read the assembly version or override.
        /// </summary>
        /// <returns></returns>
        public static SemanticVersion GetVersion()
        {
            if (_versionOverride != null)
            {
                return _versionOverride;
            }

            if (_version == null)
            {
                lock (_lock)
                {
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
                }
            }

            return _version;
        }
    }
}