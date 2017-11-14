using System;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
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
    }
}
