using Microsoft.Playwright;

namespace Sleet.UX.Tests
{
    /// <summary>
    /// Starts an installed Chromium based browser. Playwright can download its own
    /// browsers, but Chrome and Edge are already on most machines and on the GitHub
    /// hosted runners, so the tests use those instead.
    /// </summary>
    internal static class BrowserLauncher
    {
        public const string BrowserPathVariable = "SLEET_UX_BROWSER";

        public static async Task<IBrowser> LaunchAsync(IPlaywright playwright)
        {
            var path = Environment.GetEnvironmentVariable(BrowserPathVariable);

            if (!string.IsNullOrWhiteSpace(path))
            {
                return await playwright.Chromium.LaunchAsync(new() { ExecutablePath = path });
            }

            var errors = new List<string>();

            // Google Chrome, Microsoft Edge, and last the browser that playwright.ps1 install
            // adds, which is used when no channel is given.
            foreach (var channel in new[] { "chrome", "msedge", null })
            {
                try
                {
                    return await playwright.Chromium.LaunchAsync(new() { Channel = channel });
                }
                catch (PlaywrightException ex)
                {
                    var firstLine = ex.Message.Split('\n')[0].Trim();
                    errors.Add($"{channel ?? "playwright chromium"}: {firstLine}");
                }
            }

            throw new InvalidOperationException(
                $"The UX tests need Google Chrome or Microsoft Edge. Install one of them, or set {BrowserPathVariable} to the path of a Chromium based browser."
                + Environment.NewLine
                + string.Join(Environment.NewLine, errors));
        }
    }
}
