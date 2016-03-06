using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace Sleet
{
    public static class Constants
    {
        public static readonly SemanticVersion SleetVersion = SemanticVersion.Parse("0.1.0-alpha.1");

        public static readonly string Xsd = "http://www.w3.org/2001/XMLSchema#";
        public static readonly string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        public static readonly string Rdfs = "http://www.w3.org/2000/01/rdf-schema#";

        public static readonly string StringUri = "http://www.w3.org/2001/XMLSchema#string";
        public static readonly string BooleanUri = "http://www.w3.org/2001/XMLSchema#boolean";
        public static readonly string IntegerUri = "http://www.w3.org/2001/XMLSchema#integer";
        public static readonly string DateTimeUri = "http://www.w3.org/2001/XMLSchema#dateTime";

        public static readonly string SleetSchema = "http://schema.emgarten.com/sleet#";

        public static readonly Uri TypeUri = new Uri(Rdf + "type");

        public static Uri GetSleetType(string name)
        {
            return new Uri(SleetSchema + name);
        }
    }
}
