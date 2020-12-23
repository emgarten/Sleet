using System;
using System.Collections.Generic;
using FluentAssertions;
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
            settings.SymbolsEnabled.Should().BeFalse();
        }

        [Fact]
        public void GivenThatICallLoadSettingsVerifyValues()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "catalogenabled", "false" },
                { "symbolsfeedenabled", "false" },
                { "catalogpagesize", "5" },
            };

            var settings = FeedSettingsUtility.LoadSettings(values);

            settings.CatalogEnabled.Should().BeFalse();
            settings.CatalogPageSize.Should().Be(5);
            settings.SymbolsEnabled.Should().BeFalse();
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
            settings.SymbolsEnabled.Should().BeFalse();
        }

        [Fact]
        public void GivenThatICallLoadSettingsWithPageSizeOutRangeVerifyValues()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "catalogpagesize", "0" },
                { "catalogenabled", "x" },
                { "symbolsfeedenabled", "asf" },
            };

            var settings = FeedSettingsUtility.LoadSettings(values);

            settings.CatalogPageSize.Should().Be(1);
            settings.CatalogEnabled.Should().BeFalse();
            settings.SymbolsEnabled.Should().BeFalse();
        }

        [Fact]
        public void GivenThatICallLoadFeedSettingsVerifyValues()
        {
            var settings = new FeedSettings()
            {
                CatalogEnabled = false,
                CatalogPageSize = 5,
                SymbolsEnabled = false,
                BadgesEnabled = false,
            };

            var values = FeedSettingsUtility.LoadSettings(settings);

            var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "catalogenabled", "false" },
                { "symbolsfeedenabled", "false" },
                { "catalogpagesize", "5" },
                { "badgesenabled", "false" },
            };

            values.ShouldBeEquivalentTo(expected);
        }

        [Fact]
        public void GivenThatICallLoadFeedSettingsVerifyReverseValues()
        {
            var settings = new FeedSettings()
            {
                CatalogEnabled = true,
                CatalogPageSize = 10,
                SymbolsEnabled = true,
                BadgesEnabled = true
            };

            var values = FeedSettingsUtility.LoadSettings(settings);

            var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "catalogenabled", "true" },
                { "symbolsfeedenabled", "true" },
                { "catalogpagesize", "10" },
                { "badgesenabled", "true" },
            };

            values.ShouldBeEquivalentTo(expected);
        }

        [Fact]
        public void GivenThatIReadSettingsFromAnEmptyJsonFileVerifyEmptySettings()
        {
            var json = JsonUtility.Create(new Uri("http://tempuri.org/settings.json"), "Settings");

            var actual = FeedSettingsUtility.LoadSettings(FeedSettingsUtility.GetSettings(json));

            var expected = new FeedSettings();

            actual.ShouldBeEquivalentTo(expected);
        }

        [Fact]
        public void GivenThatIWriteSettingsVerifyTheyPersist()
        {
            var json = JsonUtility.Create(new Uri("http://tempuri.org/settings.json"), "Settings");

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "catalogenabled", "true" },
                { "symbolsfeedenabled", "true" },
                { "catalogpagesize", "10" },
                { "badgesenabled", "true" },
            };

            FeedSettingsUtility.Set(json, values);

            var actual = FeedSettingsUtility.GetSettings(json);

            actual.ShouldBeEquivalentTo(values);
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

            actual.ShouldBeEquivalentTo(values);
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

            actual.ShouldBeEquivalentTo(values2);
        }
    }
}
