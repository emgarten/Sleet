using AwesomeAssertions;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Sleet.UX.Tests
{
    // Anyone who can push to a feed controls the metadata the gallery shows.
    [Collection(GalleryCollection.Name)]
    public class GallerySecurityTests : GalleryTest
    {
        public GallerySecurityTests(GalleryFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task GivenMarkupInMetadataVerifyItIsShownAsText()
        {
            await OpenAsync("#package/Contoso.Hostile/1.0.0");

            await Expect(Body.Locator(".sub").First).ToHaveTextAsync("<b>Hostile</b>");
            await Expect(Body.Locator(".desc")).ToHaveTextAsync("<img src=x onerror=\"window.__sleetPwned=1\">");
            await Expect(Body.Locator(".sub").Nth(1)).ToHaveTextAsync("by <script>window.__sleetPwned=2</script>");
            await Expect(Body.Locator(".tags .tag")).ToHaveTextAsync("<i>tag</i>");
            await Expect(Body.Locator("section.group td").First).ToHaveTextAsync("<b>Dependency</b>");

            await Expect(Body.Locator("img, script, b, i, iframe, object, embed")).ToHaveCountAsync(0);
            (await Page.EvaluateAsync<bool>("() => window.__sleetPwned === undefined")).Should().BeTrue();
        }

        [Fact]
        public async Task GivenScriptUrlsInMetadataVerifyTheyAreNotLinked()
        {
            await OpenAsync("#package/Contoso.Hostile/1.0.0");
            await Expect(Body.Locator(".sub").Last).ToHaveTextAsync("Download .nupkg · View .nuspec");

            await Expect(Link("Project")).ToHaveCountAsync(0);
            await Expect(Link("License")).ToHaveCountAsync(0);
            await Expect(Body.Locator("a[href^='javascript:' i], a[href^='data:' i]")).ToHaveCountAsync(0);
        }

        // Html tags that markdown allows are left out and the rest are shown as
        // written. An image is only shown when its url is absolute.
        [Fact]
        public async Task GivenHtmlInAReadmeVerifyItIsNotRendered()
        {
            await OpenAsync("#package/Contoso.Hostile/1.0.0");

            await Expect(Readme.Locator("p").First).ToHaveTextAsync(
                "<script>window.__sleetPwned=5</script> <iframe src=\"javascript:window.__sleetPwned=7\"></iframe>");

            await Expect(Body.Locator("img, script, b, i, iframe, object, embed")).ToHaveCountAsync(0);
            (await Page.EvaluateAsync<bool>("() => window.__sleetPwned === undefined")).Should().BeTrue();
        }

        [Fact]
        public async Task GivenScriptUrlsInAReadmeVerifyTheyAreNotLinked()
        {
            await OpenAsync("#package/Contoso.Hostile/1.0.0");

            await Expect(Readme.Locator("p").Last).ToHaveTextAsync(
                "script link data link html link javascript:window.__sleetPwned=10 script image");

            await Expect(Readme.Locator("a, img")).ToHaveCountAsync(0);
            await Expect(Body.Locator("a[href^='javascript:' i], a[href^='data:' i]")).ToHaveCountAsync(0);
        }
    }
}
