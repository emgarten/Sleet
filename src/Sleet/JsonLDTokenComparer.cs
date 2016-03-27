using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Sleet
{
    /// <summary>
    /// Matches the json sort order of nuget.org
    /// </summary>
    public class JsonLDTokenComparer : IComparer<JToken>
    {
        private const string AtSymbol = "@";
        private const string Type = "@type";
        private const string Id = "@id";
        private const string Context = "@context";

        public static JsonLDTokenComparer Instance { get; } = new JsonLDTokenComparer();

        /// <summary>
        /// Apply json-ld formatting
        /// </summary>
        public static JObject Format(JObject json)
        {
            var children = json.Children().ToList();
            children.Sort(Instance);

            var formatted = new JObject();
            foreach (var child in children)
            {
                var childObj = child as JObject;
                var childArray = child as JArray;

                if (childObj != null)
                {
                    formatted.Add(Format(childObj));
                }
                else
                {
                    formatted.Add(child);
                }
            }

            return formatted;
        }

        public int Compare(JToken x, JToken y)
        {
            var xProp = x as JProperty;
            var yProp = y as JProperty;

            if (xProp != null && yProp == null)
            {
                return -1;
            }

            if (xProp == null && yProp != null)
            {
                return 1;
            }

            if (xProp != null && yProp != null)
            {
                if (xProp.Name.Equals(Id, StringComparison.Ordinal))
                {
                    return -1;
                }

                if (yProp.Name.Equals(Id, StringComparison.Ordinal))
                {
                    return 1;
                }

                if (xProp.Name.Equals(Type, StringComparison.Ordinal))
                {
                    return -1;
                }

                if (yProp.Name.Equals(Type, StringComparison.Ordinal))
                {
                    return 1;
                }

                if (xProp.Name.Equals(Context, StringComparison.Ordinal))
                {
                    return 1;
                }

                if (yProp.Name.Equals(Context, StringComparison.Ordinal))
                {
                    return -1;
                }

                var xValArray = xProp.Value as JArray;
                var yValArray = yProp.Value as JArray;

                if (xValArray == null && yValArray != null)
                {
                    return -1;
                }

                if (xValArray != null && yValArray == null)
                {
                    return 1;
                }

                if (xProp.Name.StartsWith(AtSymbol, StringComparison.Ordinal) 
                    && !yProp.Name.StartsWith(AtSymbol, StringComparison.Ordinal))
                {
                    return 1;
                }

                if (!xProp.Name.StartsWith(AtSymbol, StringComparison.Ordinal) 
                    && yProp.Name.StartsWith(AtSymbol, StringComparison.Ordinal))
                {
                    return -1;
                }

                return StringComparer.OrdinalIgnoreCase.Compare(xProp.Name, yProp.Name);
            }

            return 0;
        }
    }
}
