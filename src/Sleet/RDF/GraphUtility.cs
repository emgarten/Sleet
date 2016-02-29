using System;
using System.IO;
using System.Reflection;
using JsonLD.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sleet
{
    public static class GraphUtility
    {
        public static JToken CreateJson(BasicGraph graph, JToken context, Uri frameSchemaType)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (frameSchemaType == null)
            {
                throw new ArgumentNullException(nameof(frameSchemaType));
            }

            var quads = new JValue(graph.NQuads);
            var jObj = (JArray)JsonLdProcessor.FromRDF(quads);

            FixJsonTypes(jObj);

            var frame = new JObject();
            frame["@context"] = context;
            frame["@type"] = frameSchemaType.AbsoluteUri;

            var options = new JsonLdOptions();

            var flattened = JsonLdProcessor.Flatten(jObj, context, options);
            var framed = JsonLdProcessor.Frame(flattened, frame, options);
            var compacted = JsonLdProcessor.Compact(framed, context, options);

            var formatted = JsonLDTokenComparer.Format(compacted);

            return formatted;
        }

        /// <summary>
        /// Convert ints and bools from strings based on the RDF data type.
        /// </summary>
        /// <param name="json"></param>
        public static void FixJsonTypes(JArray json)
        {
            foreach (var item in json)
            {
                var itemObj = (JObject)item;

                foreach (var node in itemObj.Properties())
                {
                    var nodeChildren = node.Value as JArray;

                    if (nodeChildren != null)
                    {
                        foreach (var arrayItem in nodeChildren)
                        {
                            var type = SafeGetValue(arrayItem, "@type");

                            if (type == null || Constants.StringUri.Equals(type, StringComparison.Ordinal))
                            {
                                var val = SafeGetValue(arrayItem, "@value");

                                if (val != null && val.IndexOf("\\\\") > -1)
                                {
                                    // Remove double escaping, work around for json-ld bug
                                    arrayItem["@value"] = val.Replace("\\\\", "\\").Replace("\\\"", "\"");
                                }
                            }
                            else if (Constants.BooleanUri.Equals(type, StringComparison.Ordinal))
                            {
                                arrayItem["@value"] = arrayItem["@value"].ToObject<Boolean>();
                            }
                            else if (Constants.IntegerUri.Equals(type, StringComparison.Ordinal))
                            {
                                arrayItem["@value"] = arrayItem["@value"].ToObject<int>();
                            }
                        }
                    }
                }
            }
        }

        public static string SafeGetValue(JToken json, string property)
        {
            if (json.Type == JTokenType.Object)
            {
                var val = json[property];

                if (val != null)
                {
                    return val.ToObject<string>();
                }
            }

            return null;
        }

        public static BasicGraph GetGraphFromCompacted(JToken compacted)
        {
            var flattened = JsonLdProcessor.Flatten(compacted, new JsonLdOptions());
            return GetGraph(flattened);
        }

        public static BasicGraph GetGraph(JToken flattened)
        {
            var graph = new BasicGraph();
            var dataSet = (RDFDataset)JsonLdProcessor.ToRDF(flattened);

            foreach (var graphName in dataSet.GraphNames())
            {
                foreach (var quad in dataSet.GetQuads(graphName))
                {
                    graph.Assert(quad);
                }
            }

            return graph;
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
