using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Sleet.UX.Tests
{
    /// <summary>
    /// Opens a new browser page for each test and fails the test if the gallery
    /// throws an unhandled script error or its Content-Security-Policy blocks
    /// anything the gallery does.
    /// </summary>
    public abstract class GalleryTest : IAsyncLifetime
    {
        protected const string FeedUrl = GalleryFixture.FeedUrl;

        protected static readonly Regex PublishedDate = new(@"^\w{3} \d{1,2}, \d{4}$");

        private readonly ConcurrentQueue<string> _pageErrors = new();

        protected GalleryTest(GalleryFixture fixture)
        {
            Fixture = fixture;
        }

        protected GalleryFixture Fixture { get; }

        protected IBrowserContext Context { get; private set; }

        protected IPage Page { get; private set; }

        protected ILocator Body => Page.Locator("#body");

        protected ILocator Count => Page.Locator("#count");

        protected ILocator Search => Page.Locator("#search");

        protected ILocator PackageIds => Body.Locator(".pkg-id");

        protected ILocator Pager => Body.Locator("nav.pager");

        protected ILocator PageNumber => Pager.Locator(".meta");

        protected ILocator Crumbs => Body.Locator("nav.crumbs");

        protected ILocator Heading => Body.Locator(".pkg-head h2");

        protected ILocator VersionLabel => Body.Locator(".pkg-head .ver");

        protected ILocator Readme => Body.Locator(".readme");

        public async ValueTask InitializeAsync()
        {
            Context = await Fixture.NewContextAsync();
            await Context.AddInitScriptAsync(
                "window.__sleetViolations = [];" +
                "document.addEventListener('securitypolicyviolation', e => " +
                "window.__sleetViolations.push(e.violatedDirective + ' ' + e.blockedURI));");

            Page = await Context.NewPageAsync();
            Page.PageError += (_, error) => _pageErrors.Enqueue(error);
        }

        public async ValueTask DisposeAsync()
        {
            var violations = Array.Empty<string>();

            if (Context != null)
            {
                try
                {
                    if (Page != null)
                    {
                        violations = await TakeViolationsAsync();
                    }
                }
                finally
                {
                    await Context.CloseAsync();
                }
            }

            _pageErrors.Should().BeEmpty("the gallery should not throw script errors");
            violations.Should().BeEmpty("the gallery should only use what its Content-Security-Policy allows");
        }

        protected Task OpenAsync(string hash = "")
        {
            return OpenUrlAsync(FeedUrl + hash);
        }

        protected async Task OpenUrlAsync(string url)
        {
            var response = await Page.GotoAsync(url);
            response.Status.Should().Be(200, url);
        }

        /// <summary>
        /// A link in the gallery, matched by its full text.
        /// </summary>
        protected ILocator Link(string name)
        {
            return Body.GetByRole(AriaRole.Link, new() { Name = name, Exact = true });
        }

        /// <summary>
        /// The row for a package id in the list.
        /// </summary>
        protected ILocator Row(string id)
        {
            return Body.Locator(".pkg").Filter(new() { Has = Page.GetByRole(AriaRole.Link, new() { Name = id, Exact = true }) });
        }

        /// <summary>
        /// The row for a version on the versions page of a package.
        /// </summary>
        protected ILocator VersionRow(string version)
        {
            return Body.Locator("tr").Filter(new() { Has = Page.GetByRole(AriaRole.Link, new() { Name = version, Exact = true }) });
        }

        protected Task<double> GetScrollYAsync()
        {
            return Page.EvaluateAsync<double>("() => window.scrollY");
        }

        protected Task WaitForScrollYAsync(double y)
        {
            return Page.WaitForFunctionAsync("y => Math.abs(window.scrollY - y) < 1", y);
        }

        /// <summary>
        /// Requests a url from the page, through the same routes as the gallery.
        /// </summary>
        protected Task<int> GetStatusAsync(string url)
        {
            return Page.EvaluateAsync<int>("url => fetch(url).then(r => r.status)", url);
        }

        /// <summary>
        /// What the page's Content-Security-Policy has blocked since the last
        /// call, as the directive followed by the blocked url.
        /// </summary>
        protected Task<string[]> TakeViolationsAsync()
        {
            return Page.EvaluateAsync<string[]>("() => (window.__sleetViolations || []).splice(0)");
        }
    }
}
