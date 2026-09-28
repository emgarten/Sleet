using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Newtonsoft.Json.Linq;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    [Collection(EnvironmentVariableCollection.Name)]
    public class LocalSettingsTests
    {
        [Fact]
        public void LocalSettings_VerifyFeedLockTimeout()
        {
            var json = new JObject();
            json["config"] = new JObject();
            json["config"]["feedLockTimeoutMinutes"] = "30";

            var settings = LocalSettings.Load(json);

            settings.FeedLockTimeout.TotalMinutes.Should().Be(30);
        }

        [Fact]
        public void LocalSettings_VerifyEmptyFeedLockTimeout()
        {
            var json = new JObject();
            json["config"] = new JObject();
            json["config"]["feedLockTimeoutMinutes"] = "";

            var settings = LocalSettings.Load(json);

            settings.FeedLockTimeout.Should().Be(TimeSpan.MaxValue);
        }

        [Fact]
        public void LocalSettings_VerifyZeroFeedLockTimeout()
        {
            var json = new JObject();
            json["config"] = new JObject();
            json["config"]["feedLockTimeoutMinutes"] = "0";

            var settings = LocalSettings.Load(json);

            settings.FeedLockTimeout.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void LocalSettings_VerifyNoTimeoutSetting()
        {
            var json = new JObject();
            json["config"] = new JObject();

            var settings = LocalSettings.Load(json);

            settings.FeedLockTimeout.Should().Be(TimeSpan.MaxValue);
        }

        [Fact]
        public void LocalSettings_VerifyNoConfigSetting()
        {
            var json = new JObject();

            var settings = LocalSettings.Load(json);

            settings.FeedLockTimeout.Should().Be(TimeSpan.MaxValue);
        }

        [Fact]
        public void LocalSettings_Load_WithNonePath_LoadsEnvironmentConfig()
        {
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SLEET_FEED_TYPE"] = "local",
                ["SLEET_FEED_PATH"] = "relative-feed",
                ["SLEET_FEED_USERNAME"] = "mapped-user"
            };

            using (new EnvironmentVariableScope(("SLEET_FEED_TYPE", null), ("SLEET_FEED_PATH", null)))
            {
                var settings = LocalSettings.Load("none", mappings);

                settings.Path.Should().BeNull();
                settings.Json["username"].ToString().Should().Be("mapped-user");
                settings.Json["sources"][0]["name"].ToString().Should().Be("environment_feed");
                settings.Json["sources"][0]["type"].ToString().Should().Be("local");
                settings.Json["sources"][0]["path"].ToString().Should().Be("relative-feed");
            }
        }

        [Fact]
        public void LocalSettings_Load_WithNoConfigOrEnvironment_ThrowsInvalidOperationException()
        {
            using (new EnvironmentVariableScope(("SLEET_FEED_TYPE", null), ("SLEET_FEED_PATH", null)))
            {
                Action act = () => LocalSettings.Load("none");

                act.Should().Throw<InvalidOperationException>()
                    .WithMessage("Unable to find source settings. Specify the path to a sleet.json settings file.");
            }
        }

        [Fact]
        public void LocalSettings_Load_WithJsonAndPath_PreservesPath()
        {
            var json = new JObject();

            var settings = LocalSettings.Load(json, "settings-path");

            settings.Json.Should().BeSameAs(json);
            settings.Path.Should().Be("settings-path");
        }
    }
}
