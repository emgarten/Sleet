using AwesomeAssertions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Sleet.UX.Tests
{
    [Collection(GalleryCollection.Name)]
    public class GalleryNavigationTests : GalleryTest
    {
        public GalleryNavigationTests(GalleryFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task GivenAScrolledListVerifyNextPageStartsAtTopAndBackRestoresPosition()
        {
            await OpenAsync();

            var next = Link("Next →");
            await next.EvaluateAsync("e => e.scrollIntoView({ block: 'center' })");
            var saved = await GetScrollYAsync();
            saved.Should().BeGreaterThan(0);

            await next.ClickAsync();
            await Expect(PageNumber).ToHaveTextAsync("Page 2 of 3");
            (await GetScrollYAsync()).Should().Be(0);

            await Page.GoBackAsync();
            await Expect(PageNumber).ToHaveTextAsync("Page 1 of 3");
            await WaitForScrollYAsync(saved);
        }

        [Fact]
        public async Task GivenAScrolledListVerifyFilteringStartsAtTheTop()
        {
            await OpenAsync();

            await Link("Next →").EvaluateAsync("e => e.scrollIntoView({ block: 'center' })");
            (await GetScrollYAsync()).Should().BeGreaterThan(0);

            // The header stays in view, so the filter can be used from anywhere in the list.
            await Search.FillAsync("fabrikam");
            await Expect(Count).ToHaveTextAsync("40 of 45 packages");
            (await GetScrollYAsync()).Should().Be(0);
        }

        [Fact]
        public async Task GivenAPackageOpenedFromAScrolledListVerifyBackReturnsToTheSamePlace()
        {
            await OpenAsync();

            var package = Link("Fabrikam.Package15");
            await package.EvaluateAsync("e => e.scrollIntoView({ block: 'center' })");
            var saved = await GetScrollYAsync();
            saved.Should().BeGreaterThan(0);

            await package.ClickAsync();
            await Expect(Heading).ToHaveTextAsync("Fabrikam.Package15");

            await Page.GoBackAsync();
            await Expect(Link("Fabrikam.Package15")).ToBeVisibleAsync();
            await WaitForScrollYAsync(saved);
        }

        [Fact]
        public async Task GivenTheSleetLinkVerifyItOpensTheFirstPageAndBackReturns()
        {
            await OpenAsync("#q=fabrikam&page=2");
            await Expect(PageNumber).ToHaveTextAsync("Page 2 of 2");

            await Page.GetByRole(AriaRole.Link, new() { Name = "Sleet", Exact = true }).ClickAsync();
            await Expect(Count).ToHaveTextAsync("45 packages");
            await Expect(Search).ToHaveValueAsync("");
            await Expect(PageNumber).ToHaveTextAsync("Page 1 of 3");

            await Page.GoBackAsync();
            await Expect(Search).ToHaveValueAsync("fabrikam");
            await Expect(PageNumber).ToHaveTextAsync("Page 2 of 2");
        }

        [Fact]
        public async Task GivenAPackageOpenedFromAFilteredPageVerifyAllPackagesReturnsToThatPage()
        {
            await OpenAsync("#q=fabrikam&page=2");
            await Link("Fabrikam.Package25").ClickAsync();
            await Expect(Crumbs).ToHaveTextAsync("All packages›Fabrikam.Package25");

            await Link("1.0.0").ClickAsync();
            await Expect(Crumbs).ToHaveTextAsync("All packages›Fabrikam.Package25›1.0.0");

            await Crumbs.GetByRole(AriaRole.Link, new() { Name = "Fabrikam.Package25", Exact = true }).ClickAsync();
            await Expect(Count).ToHaveTextAsync("1 version");

            await Link("All packages").ClickAsync();
            await Expect(Page).ToHaveURLAsync(FeedUrl + "#q=fabrikam&page=2");
            await Expect(Search).ToHaveValueAsync("fabrikam");
            await Expect(PageNumber).ToHaveTextAsync("Page 2 of 2");
        }

        [Fact]
        public async Task GivenAFilterTypedOnAPackagePageVerifyOneBackReturnsToThePackage()
        {
            await OpenAsync("#package/Contoso.Core");
            await Expect(Count).ToHaveTextAsync("3 versions");

            await Search.PressSequentiallyAsync("fab");
            await Expect(Page).ToHaveURLAsync(FeedUrl + "#q=fab");
            await Expect(Count).ToHaveTextAsync("40 of 45 packages");

            // Each key replaces the entry that the first one added.
            await Page.GoBackAsync();
            await Expect(Page).ToHaveURLAsync(FeedUrl + "#package/Contoso.Core");
            await Expect(Count).ToHaveTextAsync("3 versions");
        }

        [Fact]
        public async Task GivenTheGalleryLoadsVerifySearchIsNotFocused()
        {
            await OpenAsync();
            await Expect(Count).ToHaveTextAsync("45 packages");

            await Expect(Search).Not.ToBeFocusedAsync();
        }

        // Addresses typed or pasted by hand can hold escapes that do not decode.
        [Theory]
        [InlineData("#package/", "#count", "45 packages")]
        [InlineData("#package/%E0%A4%A", "#body .empty", "“%E0%A4%A” is not in this feed.")]
        [InlineData("#q=%E0%A4%A", "#body .empty", "No package ids contain “%E0%A4%A”.")]
        [InlineData("#package/Contoso.Core/%E0%A4%A", "#body .note", "Version %E0%A4%A is not on this feed, showing 1.1.0 instead.")]
        public async Task GivenAMalformedAddressVerifyGalleryStillRenders(string hash, string selector, string text)
        {
            await OpenAsync(hash);

            await Expect(Page.Locator(selector)).ToHaveTextAsync(text);
        }
    }
}
