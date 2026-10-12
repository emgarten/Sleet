using System.Linq;
using AwesomeAssertions;
using Newtonsoft.Json.Linq;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class JsonLDTokenComparerTests
    {
        [Fact]
        public void Compare_WithNulls_OrdersNullBeforeNonNull()
        {
            var property = new JProperty("a", 1);

            JsonLDTokenComparer.Instance.Compare(null, null).Should().Be(0);
            JsonLDTokenComparer.Instance.Compare(null, property).Should().BeNegative();
            JsonLDTokenComparer.Instance.Compare(property, null).Should().BePositive();
        }

        [Fact]
        public void Compare_OrdersPropertiesBeforeNonProperties()
        {
            var property = new JProperty("a", 1);
            var value = new JValue(1);

            JsonLDTokenComparer.Instance.Compare(property, value).Should().BeNegative();
            JsonLDTokenComparer.Instance.Compare(value, property).Should().BePositive();
            JsonLDTokenComparer.Instance.Compare(value, new JValue(2)).Should().Be(0);
        }

        [Fact]
        public void Format_OrdersJsonLdPropertiesArraysAndAtProperties()
        {
            var json = new JObject
            {
                { "z", 1 },
                { "@context", "ctx" },
                { "items", new JArray() },
                { "@custom", "custom" },
                { "alpha", 2 },
                { "@type", "Type" },
                { "@id", "id" }
            };

            JsonLDTokenComparer.Format(json, recurse: false);

            json.Properties().Select(e => e.Name).Should().Equal("@id", "@type", "alpha", "z", "@custom", "items", "@context");
        }

        [Fact]
        public void Format_WhenRecurseTrue_DoesNotReorderObjectsInsideProperties()
        {
            var json = new JObject
            {
                { "outer", new JObject { { "b", 1 }, { "@id", "id" }, { "a", 2 } } }
            };

            JsonLDTokenComparer.Format(json);

            ((JObject)json["outer"]).Properties().Select(e => e.Name).Should().Equal("b", "@id", "a");
        }
    }
}
