using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Sleet.UX.Tests
{
    [Collection(GalleryCollection.Name)]
    public class GalleryPackageTests : GalleryTest
    {
        public GalleryPackageTests(GalleryFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task GivenAPackageVerifyVersionsPageListsEveryVersionNewestFirst()
        {
            await OpenAsync();
            await Link("Contoso.Core").ClickAsync();

            await Expect(Page).ToHaveURLAsync(FeedUrl + "#package/Contoso.Core");
            await Expect(Page).ToHaveTitleAsync("Contoso.Core · Sleet");
            await Expect(Count).ToHaveTextAsync("3 versions");
            await Expect(Crumbs).ToHaveTextAsync("Feed›Contoso.Core");
            await Expect(Heading).ToHaveTextAsync("Contoso.Core");

            var versions = Body.Locator("a.pick");
            await Expect(versions).ToHaveTextAsync(new[] { "2.0.0-Beta.1", "1.1.0", "1.0.0" });
            await Expect(versions.First).ToHaveAttributeAsync("href", "#package/Contoso.Core/2.0.0-Beta.1");
            await Expect(Body.Locator("td.meta")).ToHaveTextAsync(new[] { PublishedDate, PublishedDate, PublishedDate });

            // Only the newest stable version is the latest.
            await Expect(Body.Locator("td .tag")).ToHaveCountAsync(1);
            await Expect(VersionRow("1.1.0").Locator(".tag")).ToHaveTextAsync("latest");

            await Expect(Body.Locator(".icon-slot.lg img.icon")).ToHaveJSPropertyAsync("naturalWidth", 1);
            await Expect(Link("nupkg")).ToHaveCountAsync(0);
        }

        [Fact]
        public async Task GivenOnlyPrereleaseVersionsVerifyNewestIsTheLatest()
        {
            await OpenAsync("#package/Contoso.Preview");

            await Expect(Body.Locator("a.pick")).ToHaveTextAsync(new[] { "0.2.0-alpha", "0.1.0-alpha" });
            await Expect(Body.Locator("td .tag")).ToHaveCountAsync(1);
            await Expect(VersionRow("0.2.0-alpha").Locator(".tag")).ToHaveTextAsync("latest");
        }

        [Fact]
        public async Task GivenAVersionVerifyDetailsShowItsMetadata()
        {
            await OpenAsync("#package/Contoso.Core");
            await Link("1.1.0").ClickAsync();

            await Expect(Page).ToHaveURLAsync(FeedUrl + "#package/Contoso.Core/1.1.0");
            await Expect(Page).ToHaveTitleAsync("Contoso.Core 1.1.0 · Sleet");
            await Expect(Crumbs).ToHaveTextAsync("Feed›Contoso.Core›1.1.0");
            await Expect(Count).ToBeEmptyAsync();
            await Expect(Heading).ToHaveTextAsync("Contoso.Core");
            await Expect(VersionLabel).ToHaveTextAsync("1.1.0");
            await Expect(Body.Locator(".desc")).ToHaveTextAsync("Core types for Contoso.");

            await Expect(Body.Locator(".sub")).ToHaveTextAsync(new[]
            {
                Exactly("Contoso Core"),
                Exactly("by Contoso, Fabrikam"),
                new Regex(@"^Published \w{3} \d{1,2}, \d{4}$"),
                Exactly("project · nupkg · nuspec"),
            });

            await Expect(Link("project")).ToHaveAttributeAsync("href", "https://example.com/contoso");
            await Expect(Body.Locator(".tags .tag")).ToHaveTextAsync(new[] { "contoso", "core" });
            await Expect(Body.Locator(".icon-slot.lg img.icon")).ToHaveJSPropertyAsync("naturalWidth", 1);
        }

        [Theory]
        [InlineData("Contoso.Core", "1.1.0", "contoso.core/1.1.0/contoso.core.1.1.0.nupkg", "contoso.core/1.1.0/contoso.core.nuspec")]
        [InlineData("Contoso.Core", "2.0.0-Beta.1", "contoso.core/2.0.0-beta.1/contoso.core.2.0.0-beta.1.nupkg", "contoso.core/2.0.0-beta.1/contoso.core.nuspec")]
        [InlineData("Contoso.Meta", "1.0.0", "contoso.meta/1.0.0/contoso.meta.1.0.0.nupkg", "contoso.meta/1.0.0/contoso.meta.nuspec")]
        public async Task GivenAVersionVerifyDownloadLinksPointAtTheFeedFiles(string id, string version, string nupkg, string nuspec)
        {
            await OpenAsync($"#package/{id}/{version}");
            await Expect(VersionLabel).ToHaveTextAsync(version);

            foreach (var (name, path) in new[] { ("nupkg", nupkg), ("nuspec", nuspec) })
            {
                var url = FeedUrl + "flatcontainer/" + path;
                await Expect(Link(name)).ToHaveAttributeAsync("href", url);
                (await GetStatusAsync(url)).Should().Be(200, url);
            }
        }

        [Fact]
        public async Task GivenDependencyGroupsVerifyEachFrameworkIsShownSeparately()
        {
            await OpenAsync("#package/Contoso.Core/1.1.0");

            var groups = Body.Locator("section.group");
            await Expect(groups.Locator("h4")).ToHaveTextAsync(new[] { "net472", "net8.0", "netstandard2.0" });
            await Expect(groups.Nth(0).Locator("p")).ToHaveTextAsync("No dependencies.");
            await Expect(groups.Nth(1).Locator("tr")).ToHaveTextAsync(new[] { "Contoso.Util[1.0.0, 2.0.0)" });
            await Expect(groups.Nth(2).Locator("tr")).ToHaveTextAsync(new[] { "contoso.util[1.0.0, )", "Newtonsoft.Json[13.0.1, )" });

            // Only dependencies that are on this feed link to a package page.
            var links = groups.Nth(2).GetByRole(AriaRole.Link);
            await Expect(links).ToHaveTextAsync(new[] { "contoso.util" });
            await Expect(links).ToHaveAttributeAsync("href", "#package/Contoso.Util");

            await groups.Nth(1).GetByRole(AriaRole.Link).ClickAsync();
            await Expect(Page).ToHaveURLAsync(FeedUrl + "#package/Contoso.Util");
            await Expect(Heading).ToHaveTextAsync("Contoso.Util");
        }

        [Fact]
        public async Task GivenAReadmeVerifyItIsShownAsMarkdown()
        {
            await OpenAsync("#package/Contoso.Core/1.1.0");

            await Expect(Readme.Locator("h1")).ToHaveTextAsync("Contoso.Core");
            await Expect(Readme.Locator("h2")).ToHaveTextAsync("Install");
            await Expect(Readme.Locator("strong")).ToHaveTextAsync("Contoso");
            await Expect(Readme.Locator("pre > code")).ToHaveTextAsync("dotnet add package Contoso.Core");
            await Expect(Readme.Locator("li")).ToHaveTextAsync(new[] { "Fast", "Small" });
            await Expect(Readme.Locator("li input")).ToBeCheckedAsync();
            await Expect(Readme.Locator("li input")).ToBeDisabledAsync();
            await Expect(Readme.Locator("th")).ToHaveTextAsync(new[] { "Type", "Size" });
            await Expect(Readme.Locator("td")).ToHaveTextAsync(new[] { "Widget", "2 KB" });
            await Expect(Readme.Locator("td > code")).ToHaveTextAsync("Widget");
            await Expect(Readme.Locator("td").Last).ToHaveCSSAsync("text-align", "right");
            await Expect(Readme.Locator(".alert-note > p")).ToHaveTextAsync(new[] { "Note", "Needs .NET 8 or later." });

            var guide = Link("guide");
            await Expect(guide).ToHaveAttributeAsync("href", "https://example.com/guide");
            await Expect(guide).ToHaveAttributeAsync("title", "Contoso guide");
            await Expect(guide).ToHaveAttributeAsync("rel", "nofollow noreferrer");

            await Expect(Body.Locator("section.panel > h3")).ToHaveTextAsync(new[] { "Dependencies", "Readme" });
        }

        // Readme images load lazily, so they are scrolled to first.
        [Fact]
        public async Task GivenReadmeImagesVerifyOnesThatCannotLoadShowTheirAltText()
        {
            await OpenAsync("#package/Contoso.Core/1.1.0");

            var images = Readme.Locator("p").Filter(new() { HasText = "Diagram" });
            await images.ScrollIntoViewIfNeededAsync();

            // The relative image is never requested and the missing one is
            // replaced once it fails.
            await Expect(images).ToHaveTextAsync("Build status Diagram");
            await Expect(images.Locator("img")).ToHaveAttributeAsync("alt", "Logo");
            await Expect(images.Locator("img")).ToHaveJSPropertyAsync("naturalWidth", 1);
        }

        [Fact]
        public async Task GivenAReadmeOverOneMegabyteVerifyItIsShownAsText()
        {
            var text = "# Large\n\n" + new string('x', 1024 * 1024);
            await Page.RouteAsync(FeedUrl + "flatcontainer/contoso.core/1.1.0/readme", route => route.FulfillAsync(new()
            {
                ContentType = "text/markdown",
                Body = text,
            }));

            await OpenAsync("#package/Contoso.Core/1.1.0");

            var readme = Body.Locator("pre.readme");
            await Expect(readme).ToBeVisibleAsync();
            (await readme.TextContentAsync()).Should().Be(text);
        }

        [Fact]
        public async Task GivenAPackageWithoutOptionalMetadataVerifyDetailsLeaveItOut()
        {
            // The readme is requested after the page is drawn, once it fails
            // nothing else is added to the page.
            var readme = await Page.RunAndWaitForResponseAsync(
                () => OpenAsync("#package/Contoso.Util/1.0.0"),
                FeedUrl + "flatcontainer/contoso.util/1.0.0/readme");

            readme.Status.Should().Be(404);

            await Expect(Body.Locator(".sub")).ToHaveTextAsync(new[]
            {
                new Regex(@"^Published \w{3} \d{1,2}, \d{4}$"),
                Exactly("nupkg · nuspec"),
            });

            await Expect(Body.Locator(".desc")).ToHaveCountAsync(0);
            await Expect(Body.Locator(".tags")).ToHaveCountAsync(0);
            await Expect(Body.Locator(".icon-slot.lg img")).ToHaveCountAsync(0);
            await Expect(Body.Locator("section.panel > p")).ToHaveTextAsync("This package has no dependencies.");
            await Expect(Readme).ToHaveCountAsync(0);
        }

        // Links from NuGet clients and people do not always use the form of the
        // version or id that is on the feed.
        [Theory]
        [InlineData("Contoso.Core/1.1", "Contoso.Core", "1.1.0")]
        [InlineData("Contoso.Core/1.1.0.0", "Contoso.Core", "1.1.0")]
        [InlineData("Contoso.Core/01.01.00", "Contoso.Core", "1.1.0")]
        [InlineData("contoso.core/1.1.0", "Contoso.Core", "1.1.0")]
        [InlineData("CONTOSO.CORE/2.0.0-beta.1", "Contoso.Core", "2.0.0-Beta.1")]
        [InlineData("Contoso.Meta/1.0.0+build.5", "Contoso.Meta", "1.0.0")]
        public async Task GivenAnEquivalentVersionVerifyDetailsShowTheFeedVersion(string path, string id, string version)
        {
            await OpenAsync("#package/" + path);

            await Expect(VersionLabel).ToHaveTextAsync(version);
            await Expect(Heading).ToHaveTextAsync(id);
            await Expect(Page).ToHaveTitleAsync($"{id} {version} · Sleet");
            await Expect(Body.Locator(".note")).ToHaveCountAsync(0);
        }

        [Fact]
        public async Task GivenAVersionThatIsNotOnTheFeedVerifyLatestIsShownWithANote()
        {
            await OpenAsync("#package/Contoso.Core/9.9.9");

            await Expect(Body.Locator(".note")).ToHaveTextAsync("Version 9.9.9 is not on this feed, showing 1.1.0 instead.");
            await Expect(VersionLabel).ToHaveTextAsync("1.1.0");
        }

        [Fact]
        public async Task GivenAnIdThatIsNotOnTheFeedVerifyMessageAndWayBack()
        {
            await OpenAsync("#package/Nope.Package");

            await Expect(Body.Locator(".empty")).ToHaveTextAsync("“Nope.Package” is not in this feed.");

            await Link("Feed").ClickAsync();
            await Expect(Count).ToHaveTextAsync("45 packages");
        }

        [Fact]
        public async Task GivenAMissingRegistrationVerifyErrorAndThatTheNextVisitTriesAgain()
        {
            const string registration = "**/registration/contoso.util/index.json";
            await Page.RouteAsync(registration, route => route.FulfillAsync(new() { Status = 404 }));

            await OpenAsync();
            await Link("Contoso.Util").ClickAsync();

            var error = Body.Locator(".err");
            await Expect(error.Locator("strong")).ToHaveTextAsync("Unable to load this package.");
            await Expect(error.Locator("p")).ToHaveTextAsync(new[]
            {
                "The feed lists Contoso.Util but has no registration for it, so its versions and metadata cannot be read.",
                "GET registration/contoso.util/index.json failed with status 404"
            });

            await Expect(Page).ToHaveTitleAsync("Contoso.Util · Sleet");

            await Page.UnrouteAsync(registration);
            await Link("Feed").ClickAsync();
            await Link("Contoso.Util").ClickAsync();
            await Expect(Count).ToHaveTextAsync("1 version");
        }

        [Fact]
        public async Task GivenARegistrationRequestFailsVerifyErrorPointsAtTheFeed()
        {
            await Page.RouteAsync("**/registration/contoso.util/index.json", route => route.FulfillAsync(new() { Status = 500 }));

            await OpenAsync("#package/Contoso.Util/1.0.0");

            await Expect(Body.Locator(".err p")).ToHaveTextAsync(new[]
            {
                "Check that the feed files are readable from " + FeedUrl + ".",
                "GET registration/contoso.util/index.json failed with status 500"
            });
        }

        // Visual Studio links to a package with the template from the service index.
        [Fact]
        public async Task GivenThePackageDetailsTemplateVerifyItOpensTheVersion()
        {
            var json = await JsonUtility.LoadJsonAsync(Path.Combine(Fixture.FeedPath, "index.json"));
            var template = json["resources"]
                .Single(e => e["@type"]?.ToString() == "PackageDetailsUriTemplate/5.1.0")["@id"]
                .ToString();

            template.Should().Be(FeedUrl + "index.html#package/{id}/{version}");

            await OpenUrlAsync(template.Replace("{id}", "Contoso.Core").Replace("{version}", "1.1.0"));

            await Expect(Heading).ToHaveTextAsync("Contoso.Core");
            await Expect(VersionLabel).ToHaveTextAsync("1.1.0");
            await Expect(Body.Locator(".note")).ToHaveCountAsync(0);
        }

        private static Regex Exactly(string text)
        {
            return new Regex("^" + Regex.Escape(text) + "$");
        }
    }
}
