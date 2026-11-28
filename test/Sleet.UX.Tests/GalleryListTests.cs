using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Sleet.UX.Tests
{
    [Collection(GalleryCollection.Name)]
    public class GalleryListTests : GalleryTest
    {
        public GalleryListTests(GalleryFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task GivenAFeedVerifyFirstPageListsIdsInOrder()
        {
            await OpenAsync();

            await Expect(Page).ToHaveTitleAsync("Sleet package feed");
            await Expect(Count).ToHaveTextAsync("45 packages");
            await Expect(PackageIds).ToHaveTextAsync(GalleryFeed.Ids.Take(20));
            await Expect(Row("Contoso.Core").Locator(".pkg-id")).ToHaveAttributeAsync("href", "#package/Contoso.Core");
            await Expect(PageNumber).ToHaveTextAsync("Page 1 of 3");
        }

        [Theory]
        [InlineData("Contoso.Core", "1.1.0", "3 versions")]
        [InlineData("Contoso.Preview", "0.2.0-alpha", "2 versions")]
        [InlineData("Contoso.Meta", "1.0.0", "1 version")]
        public async Task GivenAPackageVerifyRowShowsLatestVersionAndCount(string id, string latest, string versions)
        {
            await OpenAsync();

            await Expect(Row(id).Locator(".facts > span")).ToHaveTextAsync(new[] { latest, "·", versions });
        }

        [Fact]
        public async Task GivenPackagesWithAndWithoutIconsVerifyListShowsIconsAndPlaceholders()
        {
            await OpenAsync();

            var icon = Row("Contoso.Core").Locator("img.icon");
            await Expect(icon).ToHaveAttributeAsync("src", FeedUrl + "flatcontainer/contoso.core/1.1.0/icon");
            await Expect(icon).ToHaveJSPropertyAsync("naturalWidth", 1);

            // A package without an icon keeps the empty slot so the rows line up.
            await Expect(Row("Contoso.Util").Locator("img")).ToHaveCountAsync(0);
            await Expect(Row("Contoso.Util").Locator(".icon-slot")).ToBeVisibleAsync();
        }

        [Fact]
        public async Task GivenThreePagesVerifyNextAndPreviousMoveBetweenThem()
        {
            await OpenAsync();
            await Expect(PageNumber).ToHaveTextAsync("Page 1 of 3");
            await Expect(Link("← Previous")).ToHaveCountAsync(0);

            await Link("Next →").ClickAsync();
            await Expect(Page).ToHaveURLAsync(FeedUrl + "#page=2");
            await Expect(PageNumber).ToHaveTextAsync("Page 2 of 3");
            await Expect(PackageIds).ToHaveTextAsync(GalleryFeed.Fabrikam(16, 35));

            await Link("Next →").ClickAsync();
            await Expect(Page).ToHaveURLAsync(FeedUrl + "#page=3");
            await Expect(PackageIds).ToHaveTextAsync(GalleryFeed.Fabrikam(36, 40));
            await Expect(Link("Next →")).ToHaveCountAsync(0);

            await Link("← Previous").ClickAsync();
            await Expect(Page).ToHaveURLAsync(FeedUrl + "#page=2");
            await Expect(PageNumber).ToHaveTextAsync("Page 2 of 3");
        }

        [Theory]
        [InlineData("#page=3", "Page 3 of 3", "Fabrikam.Package36")]
        [InlineData("#page=999", "Page 3 of 3", "Fabrikam.Package36")]
        [InlineData("#page=0", "Page 1 of 3", "Contoso.Core")]
        [InlineData("#page=abc", "Page 1 of 3", "Contoso.Core")]
        [InlineData("#page=-2", "Page 1 of 3", "Contoso.Core")]
        public async Task GivenAPageInTheUrlVerifyItIsKeptInRange(string hash, string pageNumber, string first)
        {
            await OpenAsync(hash);

            await Expect(PageNumber).ToHaveTextAsync(pageNumber);
            await Expect(PackageIds.First).ToHaveTextAsync(first);
        }

        [Fact]
        public async Task GivenAFilterVerifyIdsAreMatchedIgnoringCase()
        {
            await OpenAsync();
            await Expect(Count).ToHaveTextAsync("45 packages");

            await Search.FillAsync("PACKAGE1");

            await Expect(Page).ToHaveURLAsync(FeedUrl + "#q=PACKAGE1");
            await Expect(Count).ToHaveTextAsync("10 of 45 packages");
            await Expect(PackageIds).ToHaveTextAsync(GalleryFeed.Fabrikam(10, 19));
            await Expect(Pager).ToBeEmptyAsync();
        }

        [Fact]
        public async Task GivenAFilterInTheUrlVerifySearchBoxAndListMatchIt()
        {
            await OpenAsync("#q=contoso.c");

            await Expect(Search).ToHaveValueAsync("contoso.c");
            await Expect(Count).ToHaveTextAsync("1 of 45 packages");
            await Expect(PackageIds).ToHaveTextAsync(new[] { "Contoso.Core" });
        }

        [Fact]
        public async Task GivenAFilterWithManyMatchesVerifyResultsArePaged()
        {
            await OpenAsync();
            await Expect(Count).ToHaveTextAsync("45 packages");

            await Search.FillAsync("fabrikam");
            await Expect(Count).ToHaveTextAsync("40 of 45 packages");
            await Expect(PageNumber).ToHaveTextAsync("Page 1 of 2");

            await Link("Next →").ClickAsync();
            await Expect(Page).ToHaveURLAsync(FeedUrl + "#q=fabrikam&page=2");
            await Expect(PackageIds).ToHaveTextAsync(GalleryFeed.Fabrikam(21, 40));
            await Expect(Search).ToHaveValueAsync("fabrikam");
        }

        [Fact]
        public async Task GivenAFilterWithNoMatchesVerifyMessage()
        {
            await OpenAsync();
            await Expect(Count).ToHaveTextAsync("45 packages");

            await Search.FillAsync("zzz");

            await Expect(Body.Locator(".empty")).ToHaveTextAsync("No package ids contain “zzz”.");
            await Expect(Count).ToHaveTextAsync("0 of 45 packages");
        }

        [Fact]
        public async Task GivenAnEmptyFeedVerifyMessage()
        {
            await OpenUrlAsync(GalleryFixture.EmptyFeedUrl);

            await Expect(Body.Locator(".empty")).ToHaveTextAsync("This feed has no packages yet.");
            await Expect(Count).ToHaveTextAsync("0 packages");
        }

        [Fact]
        public async Task GivenTheFeedCannotBeReadVerifyErrorShowsTheRequestThatFailed()
        {
            await Page.RouteAsync("**/sleet.packageindex.json", route => route.FulfillAsync(new() { Status = 404 }));

            await OpenAsync();

            var error = Body.Locator(".err");
            await Expect(error.Locator("strong")).ToHaveTextAsync("Unable to read this feed.");
            await Expect(error.Locator("p")).ToHaveTextAsync(new[]
            {
                "GET sleet.packageindex.json failed with status 404",
                "Check that the feed files are publicly readable from " + FeedUrl + "."
            });
        }

        // The gallery finds the feed files next to the page, whichever address it was opened with.
        [Theory]
        [InlineData(GalleryFixture.FeedUrl)]
        [InlineData(GalleryFixture.FeedUrl + "index.html")]
        [InlineData("https://sleet.test/feed")]
        [InlineData(GalleryFixture.RootFeedUrl)]
        [InlineData(GalleryFixture.RootFeedUrl + "index.html")]
        public async Task GivenAFeedAddressVerifyGalleryReadsTheFeed(string url)
        {
            await OpenUrlAsync(url);

            await Expect(Count).ToHaveTextAsync("45 packages");
            await Expect(Row("Contoso.Core").Locator("img.icon")).ToHaveJSPropertyAsync("naturalWidth", 1);

            await Link("Contoso.Core").ClickAsync();
            await Expect(Count).ToHaveTextAsync("3 versions");
        }
    }
}
