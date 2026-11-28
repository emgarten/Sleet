using Microsoft.Playwright;
using NuGet.Test.Helpers;
using Xunit;

namespace Sleet.UX.Tests
{
    [CollectionDefinition(Name)]
    public class GalleryCollection : ICollectionFixture<GalleryFixture>
    {
        public const string Name = "Gallery";
    }

    /// <summary>
    /// Creates the test feeds once, starts the browser and serves the feed files to it.
    /// </summary>
    public sealed class GalleryFixture : IAsyncLifetime
    {
        public const string FeedUrl = "https://sleet.test/feed/";

        // The same feed at the root of a domain, as with an R2 or CloudFront custom domain.
        public const string RootFeedUrl = "https://feed.sleet.test/";

        public const string EmptyFeedUrl = "https://sleet.test/empty/";

        private readonly TestFolder _folder = new TestFolder();
        private readonly List<(string Url, string Path)> _sites = new();
        private IPlaywright _playwright;

        public IBrowser Browser { get; private set; }

        public string FeedPath => Path.Combine(_folder.Root, "feed");

        public async ValueTask InitializeAsync()
        {
            var emptyPath = Path.Combine(_folder.Root, "empty");
            var packagesPath = Path.Combine(_folder.Root, "packages");

            await GalleryFeed.CreateAsync(FeedPath, FeedUrl, packagesPath, GalleryFeed.GetPackages());
            await GalleryFeed.CreateAsync(emptyPath, EmptyFeedUrl, packagesPath, Array.Empty<TestNupkg>());

            _sites.Add((FeedUrl, FeedPath));
            _sites.Add((RootFeedUrl, FeedPath));
            _sites.Add((EmptyFeedUrl, emptyPath));

            Assertions.SetDefaultExpectTimeout(10_000);

            _playwright = await Playwright.CreateAsync();
            Browser = await BrowserLauncher.LaunchAsync(_playwright);
        }

        public async ValueTask DisposeAsync()
        {
            if (Browser != null)
            {
                await Browser.DisposeAsync();
            }

            _playwright?.Dispose();
            _folder.Dispose();
        }

        /// <summary>
        /// A new browser profile that can only reach the test feeds.
        /// </summary>
        public async Task<IBrowserContext> NewContextAsync()
        {
            var context = await Browser.NewContextAsync(new()
            {
                ViewportSize = new() { Width = 1000, Height = 600 },
                Locale = "en-US",
                TimezoneId = "UTC",
                ColorScheme = ColorScheme.Light,
            });

            context.SetDefaultTimeout(15_000);
            await context.RouteAsync("**/*", ServeAsync);
            return context;
        }

        private async Task ServeAsync(IRoute route)
        {
            var path = FindFile(route.Request.Url);

            if (path == null)
            {
                await route.FulfillAsync(new() { Status = 404, ContentType = "text/plain", Body = "Not found" });
                return;
            }

            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = GetContentType(path),
                BodyBytes = await File.ReadAllBytesAsync(path)
            });
        }

        // Blob storage and S3 compare names by case, so each part of the path has
        // to match exactly even on a file system that ignores case. A folder serves
        // its index.html the way a static website endpoint does.
        private string FindFile(string url)
        {
            var uri = new Uri(url);
            var address = uri.GetLeftPart(UriPartial.Path);

            foreach (var site in _sites)
            {
                var root = site.Url.TrimEnd('/');

                if (!address.Equals(root, StringComparison.Ordinal) && !address.StartsWith(root + "/", StringComparison.Ordinal))
                {
                    continue;
                }

                var current = site.Path;

                foreach (var part in address.Substring(root.Length).Split('/', StringSplitOptions.RemoveEmptyEntries))
                {
                    var name = Uri.UnescapeDataString(part);

                    if (!Directory.Exists(current) || name == "." || name == "..")
                    {
                        return null;
                    }

                    current = Directory.EnumerateFileSystemEntries(current)
                        .FirstOrDefault(e => string.Equals(Path.GetFileName(e), name, StringComparison.Ordinal));

                    if (current == null)
                    {
                        return null;
                    }
                }

                if (Directory.Exists(current))
                {
                    current = Path.Combine(current, "index.html");
                }

                return File.Exists(current) ? current : null;
            }

            return null;
        }

        // Matches the content types Sleet sets when it uploads to Azure and S3.
        private static string GetContentType(string path)
        {
            switch (Path.GetFileName(path))
            {
                case "icon":
                    return "image/png";
                case "readme":
                    return "text/markdown";
            }

            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".html":
                    return "text/html; charset=utf-8";
                case ".json":
                    return "application/json";
                case ".nupkg":
                    return "application/zip";
                case ".nuspec":
                case ".xml":
                    return "application/xml";
                default:
                    return "application/octet-stream";
            }
        }
    }
}
