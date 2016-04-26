using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sleet
{
    public static class JsonUtility
    {
        public static JObject Create(Uri rootId, string subId, string type)
        {
            return Create(rootId, subId, new List<string>() { type });
        }

        public static JObject Create(Uri rootId, string type)
        {
            return Create(rootId, new List<string>() { type });
        }

        public static JObject Create(Uri rootId, string subId, IEnumerable<string> types)
        {
            var idUri = GetId(rootId, subId);

            return Create(idUri, types);
        }

        public static Uri GetId(Uri rootId, string subId)
        {
            return UriUtility.CreateUri($"{rootId.AbsoluteUri}#{subId}");
        }

        public static JObject Create(Uri id, IEnumerable<string> types)
        {
            var json = new JObject();

            json.Add("@id", new JValue(id.AbsoluteUri));

            JToken typeValue = null;

            if (types.Count() > 1)
            {
                typeValue = new JArray(types);
            }
            else
            {
                typeValue = new JValue(types.Single());
            }

            json.Add("@type", typeValue);

            return json;
        }

        public static void SaveJson(FileInfo file, JObject json)
        {
            if (File.Exists(file.FullName))
            {
                File.Delete(file.FullName);
            }

            using (var writer = new StreamWriter(File.OpenWrite(file.FullName), Encoding.UTF8))
            {
                // TODO: make this minified after testing
                writer.Write(json.ToString(Formatting.Indented));
            }
        }

        /// <summary>
        /// True if the file can be loaded as a JObject.
        /// </summary>
        public static bool IsJson(string path)
        {
            try
            {
                var json = LoadJson(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static JObject LoadJson(FileInfo file)
        {
            return LoadJson(file.FullName);
        }

        public static JObject LoadJson(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return LoadJson(stream);
            }
        }

        public static JObject LoadJson(Stream stream)
        {
            JObject json = null;

            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                jsonReader.DateParseHandling = DateParseHandling.None;

                json = JObject.Load(jsonReader);
            }

            return json;
        }

        public static JObject GetContext(string name)
        {
            var json = LoadJson(TemplateUtility.GetResource($"context{name}.json"));
            return (JObject)json["@context"];
        }

        /// <summary>
        /// Copy properties from one JObject to another.
        /// </summary>
        public static void CopyProperties(JObject source, JObject destination, IEnumerable<string> properties, bool skipEmpty)
        {
            foreach (var fieldName in properties)
            {
                var sourceProperty = source.Property(fieldName);

                if (sourceProperty != null)
                {
                    destination.Add(sourceProperty);
                }
                else if (!skipEmpty)
                {
                    destination.Add(fieldName, string.Empty);
                }
            }
        }

        /// <summary>
        /// Copy properties from one JObject to another.
        /// Delimited items are turned into arrays.
        /// </summary>
        public static void CopyDelimitedProperties(JObject source, JObject destination, IEnumerable<string> properties, char delimiter)
        {
            foreach (var fieldName in properties)
            {
                var sourceProperty = source.Property(fieldName);

                if (sourceProperty != null)
                {
                    var array = new JArray(sourceProperty.Value.ToObject<string>().Split(delimiter).Select(s => s.Trim()));

                    destination.Add(fieldName, array);
                }
                else
                {
                    RequireArrayWithEmptyString(destination, new[] { fieldName });
                }
            }
        }

        /// <summary>
        /// Add an array with an empty item to all properties if they do not exist
        /// </summary>
        public static void RequireArrayWithEmptyString(JObject json, IEnumerable<string> properties)
        {
            foreach (var name in properties)
            {
                var property = json.Property(name);

                var array = property?.Value as JArray;

                if (array == null || array.Count < 1)
                {
                    json[name] = new JArray(new[] { string.Empty });
                }
            }
        }
    }
}
