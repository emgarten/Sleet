using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
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

        /// <summary>
        /// Read the version property as a NuGetVersion
        /// </summary>
        public static NuGetVersion GetVersion(this JToken json)
        {
            return NuGetVersion.Parse(json.GetString("version"));
        }

        /// <summary>
        /// Read the id property
        /// </summary>
        public static string GetId(this JToken json)
        {
            return json.GetString("id");
        }

        /// <summary>
        /// Read as a package identity with an id and version property.
        /// </summary>
        public static PackageIdentity GetIdentity(this JToken json)
        {
            return new PackageIdentity(json.GetId(), json.GetVersion());
        }

        /// <summary>
        /// Read the json-ld @id as a Uri
        /// </summary>
        public static Uri GetEntityId(this JToken json)
        {
            return json["@id"].ToObject<Uri>();
        }

        /// <summary>
        /// Read the property as string.
        /// </summary>
        public static string GetString(this JToken json, string propertyName)
        {
            return json[propertyName]?.ToObject<string>();
        }

        /// <summary>
        /// Retrieve an array of JObjects
        /// </summary>
        public static JObject[] GetJObjectArray(this JToken json, string propertyName)
        {
            var results = new List<JObject>();
            var root = json as JObject;

            if (root != null)
            {
                var array = root[propertyName] as JArray;

                if (array != null)
                {
                    foreach (var entry in array)
                    {
                        results.Add((JObject)entry);
                    }
                }
            }

            return results.ToArray();
        }
    }
}
