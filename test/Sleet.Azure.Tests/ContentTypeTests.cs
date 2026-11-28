using AwesomeAssertions;

namespace Sleet.Azure.Tests
{
    public class ContentTypeTests
    {
        [AzureFact]
        public async Task GivenInitVerifyGalleryContentTypeIsHtml()
        {
            await using (var testContext = new AzureTestContext())
            {
                await testContext.InitAsync();

                await InitCommand.RunAsync(
                    testContext.LocalSettings,
                    testContext.FileSystem,
                    enableCatalog: false,
                    enableSymbols: false,
                    log: testContext.Logger,
                    token: CancellationToken.None);

                // The browser must receive text/html to render the gallery
                // instead of downloading it.
                var htmlBlob = testContext.Container.GetBlobClient("index.html");
                var htmlProperties = await htmlBlob.GetPropertiesAsync();
                htmlProperties.Value.ContentType.Should().Be("text/html; charset=utf-8");
                htmlProperties.Value.ContentEncoding.Should().Be("gzip");

                var indexBlob = testContext.Container.GetBlobClient("index.json");
                var indexProperties = await indexBlob.GetPropertiesAsync();
                indexProperties.Value.ContentType.Should().Be("application/json");

                await testContext.CleanupAsync();
            }
        }
    }
}
