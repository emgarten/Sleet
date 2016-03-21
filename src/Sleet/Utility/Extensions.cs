using System;
using System.Text;
using NuGet.Versioning;

namespace Sleet
{
    public static class Extensions
    {
        /// <summary>
        /// Convert to ISO format for json.
        /// </summary>
        public static string GetDateString(this DateTimeOffset date)
        {
            return date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        /// <summary>
        /// Returns the normalized version identity. This does not contain metadata since
        /// it is not part of the actual package identity.
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public static string ToIdentityString(this SemanticVersion version)
        {
            var formatter = new VersionFormatter();
            var format = "V";

            if (version.IsPrerelease)
            {
                format += "-R";
            }

            return version.ToString(format, formatter);
        }

        /// <summary>
        /// Returns the display version, this contains metadata and is not unique
        /// to the package identity.
        /// </summary>
        public static string ToFullVersionString(this SemanticVersion version)
        {
            var formatter = new VersionFormatter();
            var format = "V";

            if (version.IsPrerelease)
            {
                format += "-R";
            }

            if (version.HasMetadata)
            {
                format += "+M";
            }

            return version.ToString(format, formatter);
        }
    }
}
