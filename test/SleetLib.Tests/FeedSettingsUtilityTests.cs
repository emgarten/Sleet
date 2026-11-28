using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class FeedSettingsUtilityTests
    {
        [Fact]
        public void GivenThatICallLoadSettingsOnEmptySettingsVerifyDefaults()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var settings = FeedSettingsUtility.LoadSettings(values);

            // Defaults
            settings.CatalogEnabled.Should().BeFalse();
            settings.CatalogPageSize.Should().Be(1024);
        }

        [Fact]
        public void GivenThatICallLoadSettingsVerifyValues()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "catalogenabled", "false" },
                { "catalogpagesize", "5" },
            };

            var settings = FeedSettingsUtility.LoadSettings(values);

            settings.CatalogEnabled.Should().BeFalse();
            settings.CatalogPageSize.Should().Be(5);
        }

        [Fact]
        public void GivenThatICallLoadSettingsWithUnknownSettingsVerifyValues()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "a", "1" },
                { "b", "2" },
                { "c", "3" },
            };

            var settings = FeedSettingsUtility.LoadSettings(values);

            // Defaults
            settings.CatalogEnabled.Should().BeFalse();
            settings.CatalogPageSize.Should().Be(1024);
        }

        [Fact]
        public void GivenThatICallLoadSettingsWithPageSizeOutRangeVerifyValues()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "catalogpagesize", "0" },
                { "catalogenabled", "x" },
            };

            var settings = FeedSettingsUtility.LoadSettings(values);

            settings.CatalogPageSize.Should().Be(1);
            settings.CatalogEnabled.Should().BeFalse();
        }

        [Theory]
        [InlineData("yes")]
        [InlineData("1")]
        [InlineData("")]
        public void GivenThatICallLoadSettingsWithAnInvalidBadgesValueVerifyBadgesAreOff(string value)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "badgesenabled", value },
            };

            var settings = FeedSettingsUtility.LoadSettings(values);

            settings.BadgesEnabled.Should().BeFalse();
        }

        [Fact]
        public void GivenThatICallLoadFeedSettingsVerifyValues()
        {
            var settings = new FeedSettings()
            {
                CatalogEnabled = false,
                CatalogPageSize = 5,
                BadgesEnabled = false,
                GalleryEnabled = false,
            };

            var values = FeedSettingsUtility.LoadSettings(settings);

            var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "catalogenabled", "false" },
                { "catalogpagesize", "5" },
                { "badgesenabled", "false" },
                { "galleryenabled", "false" },
            };

            values.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void GivenThatICallLoadFeedSettingsVerifyReverseValues()
        {
            var settings = new FeedSettings()
            {
                CatalogEnabled = true,
                CatalogPageSize = 10,
                BadgesEnabled = true,
                GalleryEnabled = true
            };

            var values = FeedSettingsUtility.LoadSettings(settings);

            var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "catalogenabled", "true" },
                { "catalogpagesize", "10" },
                { "badgesenabled", "true" },
                { "galleryenabled", "true" },
            };

            values.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void GivenThatIReadSettingsFromAnEmptyJsonFileVerifyEmptySettings()
        {
            var json = JsonUtility.Create(new Uri("http://tempuri.org/settings.json"), "Settings");

            var actual = FeedSettingsUtility.LoadSettings(FeedSettingsUtility.GetSettings(json));

            var expected = new FeedSettings();

            actual.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void GivenThatIWriteSettingsVerifyTheyPersist()
        {
            var json = JsonUtility.Create(new Uri("http://tempuri.org/settings.json"), "Settings");

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "catalogenabled", "true" },
                { "catalogpagesize", "10" },
                { "badgesenabled", "true" },
            };

            FeedSettingsUtility.Set(json, values);

            var actual = FeedSettingsUtility.GetSettings(json);

            actual.Should().BeEquivalentTo(values);
        }

        [Fact]
        public void GivenThatIWriteSettingsVerifyTheyPersistForUnknownValues()
        {
            var json = JsonUtility.Create(new Uri("http://tempuri.org/settings.json"), "Settings");

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "a", "1" },
                { "b", "2" },
                { "c", "3" },
            };

            FeedSettingsUtility.Set(json, values);

            var actual = FeedSettingsUtility.GetSettings(json);

            actual.Should().BeEquivalentTo(values);
        }

        [Fact]
        public void GivenThatIUnsetAllVerifyNoSettings()
        {
            var json = JsonUtility.Create(new Uri("http://tempuri.org/settings.json"), "Settings");

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "a", "1" },
                { "b", "2" },
                { "c", "3" },
            };

            FeedSettingsUtility.Set(json, values);

            FeedSettingsUtility.UnsetAll(json);

            var actual = FeedSettingsUtility.GetSettings(json);

            actual.Should().BeEmpty();
        }

        [Fact]
        public void GivenThatIUnsetASingleValueVerifyItIsGone()
        {
            var json = JsonUtility.Create(new Uri("http://tempuri.org/settings.json"), "Settings");

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "a", "1" },
                { "b", "2" },
                { "c", "3" },
            };

            var values2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "a", "1" },
                { "c", "3" },
            };

            FeedSettingsUtility.Set(json, values);

            FeedSettingsUtility.Set(json, values2);

            var actual = FeedSettingsUtility.GetSettings(json);

            actual.Should().BeEquivalentTo(values2);
        }
    }
}
