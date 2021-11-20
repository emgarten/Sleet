using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sleet
{
    public static class JsonUtility
    {
        // json files MUST NOT have a BOM
        // https://tools.ietf.org/html/rfc7159#section-8.1
        private static readonly UTF8Encoding JsonEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static readonly JsonLoadSettings _jsonLoadSettings = new JsonLoadSettings()
        {
            LineInfoHandling = LineInfoHandling.Ignore,
            CommentHandling = CommentHandling.Ignore,
        };

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
            var json = new JObject
            {
                { "@id", new JValue(id.AbsoluteUri) }
            };

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

        /// <summary>
        /// Compress and remove indentation for json data
        /// </summary>
        public static async Task<MemoryStream> GZipAndMinifyAsync(Stream input)
        {
            var memoryStream = new MemoryStream();

            if (input.CanSeek)
            {
                input.Position = 0;
            }

            var json = await LoadJsonAsync(input);

            using (var zipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                await WriteJsonAsync(json, zipStream);
                await zipStream.FlushAsync();
                await memoryStream.FlushAsync();
            }

            memoryStream.Position = 0;

            return memoryStream;
        }

        public static async Task SaveJsonAsync(FileInfo file, JObject json)
        {
            if (File.Exists(file.FullName))
            {
                File.Delete(file.FullName);
            }

            using (var stream = File.OpenWrite(file.FullName))
            {
                await WriteJsonAsync(json, stream);
            }
        }

        public static async Task WriteJsonAsync(JObject json, Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (var writer = new StreamWriter(stream, JsonEncoding, bufferSize: 8192, leaveOpen: true))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                jsonWriter.Formatting = Formatting.None;
                await json.WriteToAsync(jsonWriter);
                await writer.FlushAsync();
                await jsonWriter.FlushAsync();
            }

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
        }

        /// <summary>
        /// True if the file can be loaded as a JObject.
        /// </summary>
        public static async Task<bool> IsJsonAsync(string path)
        {
            try
            {
                var json = await LoadJsonAsync(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static Task<JObject> LoadJsonAsync(FileInfo file)
        {
            return LoadJsonAsync(file.FullName);
        }

        public static async Task<JObject> LoadJsonAsync(string path)
        {
            Debug.Assert(File.Exists(path), "File must exist");

            JObject json = null;

            using (var stream = File.OpenRead(path))
            {
                json = await LoadJsonAsync(stream);
            }

            return json;
        }

        public static async Task<JObject> LoadJsonAsync(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                jsonReader.DateParseHandling = DateParseHandling.None;
                var json = await JObject.LoadAsync(jsonReader, _jsonLoadSettings);
                return json;
            }
        }

        public static async Task<JObject> GetContextAsync(string name)
        {
            var json = await LoadJsonAsync(TemplateUtility.GetResource($"context{name}.json"));
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

        /// <summary>
        /// Get the @id of a json page.
        /// </summary>
        public static Uri GetIdUri(JObject json)
        {
            var s = json["@id"].ToObject<string>();
            return UriUtility.CreateUri(s);
        }

        /// <summary>
        /// Get items from a page or index page.
        /// </summary>
        public static List<JObject> GetItems(JObject json)
        {
            var result = new List<JObject>();

            if (json["items"] is JArray items)
            {
                foreach (var item in items)
                {
                    result.Add((JObject)item);
                }
            }

            return result;
        }

        public static string GetValueCaseInsensitive(JObject obj, string name)
        {
            if (obj != null)
            {
                var val = obj.GetValue(name, StringComparison.OrdinalIgnoreCase);

                if (val != null)
                {
                    return val.ToObject<string>();
                }
            }

            return null;
        }

        public static bool GetBoolCaseInsensitive(JObject obj, string name, bool defaultValue)
        {
            if (obj != null)
            {
                var val = obj.GetValue(name, StringComparison.OrdinalIgnoreCase);

                if (val != null)
                {
                    return val.ToObject<bool>();
                }
            }

            return defaultValue;
        }
    }
}
