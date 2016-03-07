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
            return new Uri($"{rootId.AbsoluteUri}#{subId}");
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
                writer.Write(json.ToString(Formatting.None));
            }
        }

        public static JObject LoadJson(FileInfo file)
        {
            using (var stream = file.OpenRead())
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
            var json = LoadJson(GetResource($"context{name}.json"));
            return (JObject)json["@context"];
        }

        public static Stream GetResource(string name)
        {
            var path = $"Sleet.compiler.resources.{name}";
            return typeof(Program).GetTypeInfo().Assembly.GetManifestResourceStream(path);
        }
    }
}
