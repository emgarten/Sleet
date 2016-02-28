using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Sleet.Test
{
    public class GraphUtilityTests
    {
        [Fact]
        public void GraphUtility_GraphRoundTrip()
        {
            // Arrange
            var stream = TestUtility.GetResource("nugetCatalogPage01.json");
            var originalJson = GraphUtility.LoadJson(stream);
            var context = (JObject)originalJson["@context"];

            // Act
            var graph = GraphUtility.GetGraphFromCompacted(originalJson);

            var compacted = GraphUtility.CreateJson(
                graph,
                context,
                new Uri("http://schema.nuget.org/schema#PackageDetails"));

            // Assert
            Assert.Equal(originalJson.ToString(), compacted.ToString());
        }
    }
}
