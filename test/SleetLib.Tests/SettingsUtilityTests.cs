using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using Newtonsoft.Json.Linq;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    [Collection(EnvironmentVariableCollection.Name)]
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

        [Fact]
        public void SettingsUtility_GetTokenValue_MappingOverridesEnvironmentVariable()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SLEET_FEED_PATH"] = "mapping-value"
            };

            using (new EnvironmentVariableScope(("SLEET_FEED_PATH", "environment-value")))
            {
                SettingsUtility.GetTokenValue("SLEET_FEED_PATH", values, "default-value").Should().Be("mapping-value");
            }
        }

        [Fact]
        public void SettingsUtility_GetTokenValue_UsesDefaultForMissingOrEmptyValues()
        {
            using (new EnvironmentVariableScope(("SLEET_FEED_PATH", string.Empty)))
            {
                SettingsUtility.GetTokenValue("SLEET_FEED_PATH", mappings: null, "default-value").Should().Be("default-value");
                SettingsUtility.GetTokenValue("SLEET_FEED_MISSING", mappings: null, null).Should().BeEmpty();
            }
        }

        [Fact]
        public void SettingsUtility_GetConfigFromEnv_WithoutFeedType_ReturnsNull()
        {
            using (new EnvironmentVariableScope(("SLEET_FEED_TYPE", null), ("SLEET_FEED_PATH", "ignored")))
            {
                SettingsUtility.GetConfigFromEnv(mappings: null).Should().BeNull();
            }
        }

        [Fact]
        public void SettingsUtility_GetConfigFromEnv_UsesFeedEnvironmentVariables()
        {
            using (new EnvironmentVariableScope(
                ("SLEET_FEED_TYPE", "local"),
                ("SLEET_FEED_PATH", "environment-path"),
                ("SLEET_FEED_NAME", "ignored-name"),
                ("SLEET_FEED_BASEURI", "https://example.test/feed/"),
                ("SLEET_FEED_USERNAME", "env-user"),
                ("SLEET_FEED_USEREMAIL", "env@example.test"),
                ("SLEET_FEED_PROXY_USEDEFAULTCREDENTIALS", "true")))
            {
                var json = SettingsUtility.GetConfigFromEnv(mappings: null);

                json.Should().NotBeNull();
                json["username"].ToString().Should().Be("env-user");
                json["useremail"].ToString().Should().Be("env@example.test");
                json["proxy"]["useDefaultCredentials"].Value<bool>().Should().BeTrue();

                var source = (JObject)json["sources"].Single();
                source["name"].ToString().Should().Be("envirnoment_feed");
                source["type"].ToString().Should().Be("local");
                source["path"].ToString().Should().Be("environment-path");
                source["baseuri"].ToString().Should().Be("https://example.test/feed/");
                source.ContainsKey("name").Should().BeTrue();
            }
        }

        [Fact]
        public void SettingsUtility_GetConfigFromEnv_MappingsOverrideEnvironmentVariablesAndResolveTokens()
        {
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SLEET_FEED_TYPE"] = "local",
                ["SLEET_FEED_PATH"] = "$feed_path$",
                ["feed_path"] = "mapped-path"
            };

            using (new EnvironmentVariableScope(("SLEET_FEED_TYPE", "s3"), ("SLEET_FEED_PATH", "environment-path")))
            {
                var json = SettingsUtility.GetConfigFromEnv(mappings);

                var source = (JObject)json["sources"].Single();
                source["type"].ToString().Should().Be("local");
                source["path"].ToString().Should().Be("mapped-path");
            }
        }
    }
}
