using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
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
            return date.UtcDateTime.ToString("o");
        }

        /// <summary>
        /// Returns the normalized version identity. This does not contain metadata since
        /// it is not part of the actual package identity.
        /// </summary>
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

            // TODO: re-add this once NuGet properly supports SemVer 2.0.0 metadata.
            //if (version.HasMetadata)
            //{
            //    format += "+M";
            //}

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

            if (json is JObject root)
            {
                if (root[propertyName] is JArray array)
                {
                    foreach (var entry in array)
                    {
                        results.Add((JObject)entry);
                    }
                }
            }

            return results.ToArray();
        }

        /// <summary>
        /// Returns the @id as a URI
        /// </summary>
        public static Uri GetIdUri(this JObject json)
        {
            return JsonUtility.GetIdUri(json);
        }

        /// <summary>
        /// Save an XML file to a stream.
        /// </summary>
        public static MemoryStream AsMemoryStreamAsync(this XDocument doc)
        {
            var mem = new MemoryStream();
            doc.Save(mem);
            mem.Position = 0;
            return mem;
        }

        /// <summary>
        /// Converts a stream to a MemoryStream.
        /// Disposes of the original stream.
        /// </summary>
        public static async Task<MemoryStream> AsMemoryStreamAsync(this Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            var mem = new MemoryStream();

            using (stream)
            {
                await stream.CopyToAsync(mem);
            }

            mem.Position = 0;
            return mem;
        }

        /// <summary>
        /// Partition a list into segements of a given number.
        /// </summary>
        internal static List<List<T>> Partition<T>(this IEnumerable<T> entries, int max)
        {
            var results = new List<List<T>>();
            var set = new List<T>();

            foreach (var entry in entries)
            {
                if (set.Count >= max)
                {
                    results.Add(set);
                    set = new List<T>();
                }

                set.Add(entry);
            }

            if (set.Count > 0)
            {
                results.Add(set);
            }

            return results;
        }
    }
}
