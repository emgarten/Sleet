using AwesomeAssertions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Sleet.UX.Tests
{
    [Collection(GalleryCollection.Name)]
    public class GalleryModeTests : GalleryTest
    {
        private const string DarkBackground = "rgb(27, 26, 25)";
        private const string LightBackground = "rgb(248, 248, 248)";

        public GalleryModeTests(GalleryFixture fixture)
            : base(fixture)
        {
        }

        private ILocator Mode => Page.Locator("#mode");

        [Fact]
        public async Task GivenABrowserThatPrefersLightVerifyGalleryStartsDark()
        {
            await OpenAsync();
            await Expect(Count).ToHaveTextAsync("45 packages");

            await Expect(Page.Locator("html")).Not.ToHaveAttributeAsync("data-theme", "light");
            await Expect(Page.Locator("body")).ToHaveCSSAsync("background-color", DarkBackground);
            await Expect(Mode).ToHaveAttributeAsync("aria-label", "Switch to light mode");
        }

        [Fact]
        public async Task GivenLightModeIsChosenVerifyItIsKeptAfterReload()
        {
            await OpenAsync();
            await Expect(Count).ToHaveTextAsync("45 packages");

            await Mode.ClickAsync();
            await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-theme", "light");
            await Expect(Page.Locator("body")).ToHaveCSSAsync("background-color", LightBackground);
            await Expect(Mode).ToHaveAttributeAsync("aria-label", "Switch to dark mode");

            await OpenAsync("#package/Contoso.Core");
            await Expect(Count).ToHaveTextAsync("3 versions");
            await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-theme", "light");
            await Expect(Mode).ToHaveAttributeAsync("aria-label", "Switch to dark mode");
        }

        [Fact]
        public async Task GivenLightModeVerifyTogglingAgainReturnsToDark()
        {
            await OpenAsync();
            await Expect(Count).ToHaveTextAsync("45 packages");

            await Mode.ClickAsync();
            await Mode.ClickAsync();
            await Expect(Page.Locator("body")).ToHaveCSSAsync("background-color", DarkBackground);
            await Expect(Mode).ToHaveAttributeAsync("aria-label", "Switch to light mode");

            await Page.ReloadAsync();
            await Expect(Count).ToHaveTextAsync("45 packages");
            await Expect(Page.Locator("body")).ToHaveCSSAsync("background-color", DarkBackground);
            (await Page.EvaluateAsync<string>("() => localStorage.getItem('sleet-gallery-mode')")).Should().Be("dark");
        }
    }
}
