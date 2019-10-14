using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class SettingsUtilityTests
    {
        [Fact]
        public void SettingsUtility_GetPropertyMappings_Null()
        {
            SettingsUtility.GetPropertyMappings(null).Count.Should().Be(0);
        }

        [Fact]
        public void SettingsUtility_GetPropertyMappings_Basic()
        {
            var values = new List<string>()
            {
                "a=b"
            };

            SettingsUtility.GetPropertyMappings(values).Count.Should().Be(1);
            SettingsUtility.GetPropertyMappings(values)["a"].Should().Be("b");
        }

        [Fact]
        public void SettingsUtility_GetPropertyMappings_Duplicates()
        {
            var values = new List<string>()
            {
                "a=b",
                "a=c"
            };

            SettingsUtility.GetPropertyMappings(values).Count.Should().Be(1);
            SettingsUtility.GetPropertyMappings(values)["a"].Should().Be("b");
        }

        [Theory]
        [InlineData("", "")]
        [InlineData(null, null)]
        [InlineData("a", "a")]
        [InlineData("a$", "a$")]
        [InlineData("$", "$")]
        [InlineData("$a$", "b")]
        [InlineData("$$a$$", "$a$")]
        [InlineData("$alskdfjsdflffffffffffffffff$", "$alskdfjsdflffffffffffffffff$")]
        [InlineData("a$b$c", "axyzc")]
        [InlineData("$x$", "$z$")]
        public void SettingsUtility_ResolveTokens_NonToken(string input, string expected)
        {
            var values = new List<string>()
            {
                "a=b",
                "b=xy$c$",
                "c=z",
                "x=$z$", // circle
                "z=$x$"
            };

            SettingsUtility.ResolveTokens(input, SettingsUtility.GetPropertyMappings(values))
                .Should()
                .Be(expected);
        }

        [Fact]
        public void SettingsUtility_ResolveTokensInJson()
        {
            var json = new JObject
            {
                ["key"] = "$a$"
            };

            var values = new List<string>()
            {
                "a=$b$",
                "b=c"
            };

            var mappings = SettingsUtility.GetPropertyMappings(values);
            SettingsUtility.ResolveTokensInSettingsJson(json, mappings);

            json["key"].ToString().Should().Be("c");
        }

        [Fact]
        public void SettingsUtility_ResolveNestedTokensInJson()
        {
            var json = new JObject
            {
                ["sources"] = new JArray(new JObject(new JProperty("key", "x$a$z")))
            };

            var values = new List<string>()
            {
                "a=$b$",
                "b=y"
            };

            var mappings = SettingsUtility.GetPropertyMappings(values);
            SettingsUtility.ResolveTokensInSettingsJson(json, mappings);

            json["sources"][0]["key"].ToString().Should().Be("xyz");
        }
    }
}
