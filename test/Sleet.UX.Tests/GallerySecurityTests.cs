using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;
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
            await Expect(Body.Locator(".sub").Last).ToHaveTextAsync("nupkg · nuspec");

            await Expect(Link("project")).ToHaveCountAsync(0);
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

        // Opened from the feed, an xml file runs as a page of the feed's site,
        // so the gallery never links to the nuspec file and reads it as text.
        [Fact]
        public async Task GivenMarkupInANuspecVerifyItIsShownAsText()
        {
            const string nuspec =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\"><metadata>" +
                "<id>Contoso.Util</id><version>1.0.0</version>" +
                "<h:script xmlns:h=\"http://www.w3.org/1999/xhtml\">window.__sleetPwned=12</h:script>" +
                "</metadata></package>";

            await Page.RouteAsync(FeedUrl + "flatcontainer/contoso.util/1.0.0/contoso.util.nuspec", route => route.FulfillAsync(new()
            {
                ContentType = "application/xml",
                Body = nuspec,
            }));

            await OpenAsync("#package/Contoso.Util/1.0.0");
            await Expect(Link("nuspec")).ToBeVisibleAsync();
            await Expect(Body.Locator("a[href*='.nuspec' i]")).ToHaveCountAsync(0);

            await Link("nuspec").ClickAsync();

            var source = Body.Locator("pre.source");
            await Expect(source).ToBeVisibleAsync();
            (await source.TextContentAsync()).Should().Be(nuspec);
            await Expect(source.Locator("*")).ToHaveCountAsync(0);
            (await Page.EvaluateAsync<bool>("() => window.__sleetPwned === undefined")).Should().BeTrue();
        }

        // A link into the feed's own site could open one of its files, such as a
        // nuspec, as a page of the site.
        [Theory]
        [InlineData("docs/home")]
        [InlineData("/feed/flatcontainer/contoso.core/1.1.0/contoso.core.nuspec")]
        [InlineData("//sleet.test/feed/flatcontainer/contoso.core/1.1.0/contoso.core.nuspec")]
        [InlineData("https://sleet.test/feed/flatcontainer/contoso.core/1.1.0/contoso.core.nuspec")]
        [InlineData("HTTPS://SLEET.TEST:443/feed/")]
        public async Task GivenAProjectUrlIntoTheFeedSiteVerifyItIsNotLinked(string projectUrl)
        {
            var json = await JsonUtility.LoadJsonAsync(Path.Combine(Fixture.FeedPath, "registration", "contoso.core", "index.json"));

            foreach (var entry in json.SelectTokens("items[*].items[*].catalogEntry"))
            {
                entry["projectUrl"] = projectUrl;
            }

            await Page.RouteAsync(FeedUrl + "registration/contoso.core/index.json", route => route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Body = json.ToString(),
            }));

            await OpenAsync("#package/Contoso.Core/1.1.0");

            await Expect(Body.Locator(".sub").Last).ToHaveTextAsync("nupkg · nuspec");
            await Expect(Link("project")).ToHaveCountAsync(0);
        }

        [Fact]
        public async Task GivenReadmeLinksIntoTheFeedSiteVerifyOnlyTheirTextIsShown()
        {
            await RouteReadmeAsync(
                "[relative](docs/guide.md) [rooted](/feed/index.json) " +
                "[same site](https://sleet.test/feed/flatcontainer/contoso.core/1.1.0/contoso.core.nuspec) " +
                "<https://sleet.test/feed/index.json> https://sleet.test/feed/sleet.settings.json " +
                "[other site](https://example.com/other)\n");

            await OpenAsync("#package/Contoso.Core/1.1.0");

            await Expect(Readme.Locator("p")).ToHaveTextAsync(
                "relative rooted same site https://sleet.test/feed/index.json https://sleet.test/feed/sleet.settings.json other site");

            await Expect(Readme.Locator("a")).ToHaveCountAsync(1);
            await Expect(Link("other site")).ToHaveAttributeAsync("href", "https://example.com/other");
        }

        // Loading an image tells its host who is reading the readme, so only the
        // hosts nuget.org trusts load before the reader asks. Readme images load
        // lazily, so they are scrolled to first.
        [Fact]
        public async Task GivenReadmeImagesFromOtherSitesVerifyTheyWaitForTheReader()
        {
            var trackerRequests = 0;
            await Page.RouteAsync("https://img.shields.io/**", FulfillImageAsync);
            await Page.RouteAsync("https://github.com/**", FulfillImageAsync);
            await Page.RouteAsync("https://images.example/**", route =>
            {
                Interlocked.Increment(ref trackerRequests);
                return FulfillImageAsync(route);
            });

            await RouteReadmeAsync(
                "![allowed](https://img.shields.io/badge/a.png) " +
                "![upgraded](http://img.shields.io/badge/b.png) " +
                "![workflow](https://github.com/contoso/core/actions/workflows/ci.yml/badge.svg) " +
                "![tracker](https://images.example/pixel.png)\n");

            await OpenAsync("#package/Contoso.Core/1.1.0");

            var images = Readme.Locator("p");
            await images.ScrollIntoViewIfNeededAsync();

            await Expect(images.Locator("img")).ToHaveCountAsync(3);
            foreach (var image in await images.Locator("img").AllAsync())
            {
                await Expect(image).ToHaveJSPropertyAsync("naturalWidth", 1);
            }

            await Expect(images.Locator("img[alt='upgraded']")).ToHaveAttributeAsync("src", "https://img.shields.io/badge/b.png");
            await Expect(images.Locator(".img-off")).ToHaveTextAsync("tracker");
            await Expect(Body.Locator(".img-bar > span")).ToHaveTextAsync("Images from other sites are hidden.");
            Volatile.Read(ref trackerRequests).Should().Be(0);

            await Body.GetByRole(AriaRole.Button, new() { Name = "Show images" }).ClickAsync();

            var tracker = images.Locator("img[alt='tracker']");
            await tracker.ScrollIntoViewIfNeededAsync();
            await Expect(tracker).ToHaveJSPropertyAsync("naturalWidth", 1);
            await Expect(tracker).ToHaveAttributeAsync("referrerpolicy", "no-referrer");
            await Expect(Body.Locator(".img-bar, .img-off")).ToHaveCountAsync(0);
            Volatile.Read(ref trackerRequests).Should().Be(1);
        }

        // Text from the address is only repeated when it reads as a package id or
        // version, so a crafted link cannot add its own message to the page.
        [Theory]
        [InlineData("#q=Visit%20evil.example%20to%20fix%20this", "#body .empty", "No package ids match the filter.", "evil.example")]
        [InlineData("#package/Call%20555-0100%20for%20support", "#body .empty", "That package is not in this feed.", "555-0100")]
        [InlineData("#package/Contoso.Core/9.9.9%20was%20pulled,%20install%20Evil.Package", "#body .note", "That version is not on this feed, showing 1.1.0 instead.", "Evil.Package")]
        public async Task GivenProseInTheAddressVerifyItIsNotRepeated(string hash, string selector, string text, string phrase)
        {
            await OpenAsync(hash);

            await Expect(Page.Locator(selector)).ToHaveTextAsync(text);
            await Expect(Body).Not.ToContainTextAsync(phrase);
            await Expect(Page).Not.ToHaveTitleAsync(new Regex(Regex.Escape(phrase)));
        }

        // Ids are looked up by name, so a package named after a property every
        // object has is only found when the feed has it.
        [Theory]
        [InlineData("constructor")]
        [InlineData("__proto__")]
        public async Task GivenAnIdNamedAfterAnObjectPropertyVerifyItIsNotFound(string id)
        {
            await OpenAsync("#package/" + id);

            await Expect(Body.Locator(".empty")).ToHaveTextAsync($"“{id}” is not in this feed.");
        }

        [Theory]
        [InlineData("constructor")]
        [InlineData("__proto__")]
        public async Task GivenAPackageNamedAfterAnObjectPropertyVerifyItOpens(string id)
        {
            var index = await JsonUtility.LoadJsonAsync(Path.Combine(Fixture.FeedPath, "sleet.packageindex.json"));
            index["packages"][id] = new JArray("1.0.0");

            await Page.RouteAsync(FeedUrl + "sleet.packageindex.json", route => route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Body = index.ToString(),
            }));

            // The package borrows the registration of another package.
            var registration = await File.ReadAllTextAsync(
                Path.Combine(Fixture.FeedPath, "registration", "contoso.util", "index.json"),
                TestContext.Current.CancellationToken);

            await Page.RouteAsync(FeedUrl + $"registration/{id}/index.json", route => route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Body = registration,
            }));

            await OpenAsync("#package/" + id);

            await Expect(Heading).ToHaveTextAsync(id);
            await Expect(Count).ToHaveTextAsync("1 version");
        }

        // The page's policy only runs the gallery's own scripts, so markup that
        // got past the gallery still could not run script.
        [Fact]
        public async Task GivenAnInjectedScriptVerifyThePolicyBlocksIt()
        {
            await OpenAsync();
            await Expect(Count).ToHaveTextAsync("45 packages");

            var directive = await Page.EvaluateAsync<string>(@"() => new Promise(resolve => {
                document.addEventListener('securitypolicyviolation', e => resolve(e.violatedDirective), { once: true });
                const script = document.createElement('script');
                script.textContent = 'window.__sleetPwned = 13';
                document.body.append(script);
            })");

            directive.Should().Be("script-src-elem");
            (await Page.EvaluateAsync<bool>("() => window.__sleetPwned === undefined")).Should().BeTrue();
            (await TakeViolationsAsync()).Should().Equal("script-src-elem inline");
        }

        private static Task FulfillImageAsync(IRoute route)
        {
            return route.FulfillAsync(new() { ContentType = "image/png", BodyBytes = GalleryFeed.IconBytes });
        }
    }
}
