using AwesomeAssertions;

namespace Sleet.AmazonS3.Tests
{
    public class ContentTypeTests
    {
        [AmazonS3Fact]
        public async Task GivenInitVerifyGalleryContentTypeIsHtml()
        {
            await using (var testContext = new AmazonS3TestContext())
            {
                await testContext.InitAsync();

                await InitCommand.RunAsync(
                    testContext.LocalSettings,
                    testContext.FileSystem,
                    enableCatalog: false,
                    log: testContext.Logger,
                    token: CancellationToken.None);

                // The browser must receive text/html to render the gallery
                // instead of downloading it.
                var htmlMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "index.html");
                htmlMetadata.Headers.ContentType.Should().Be("text/html; charset=utf-8");

                // Cloud Storage decompresses gzip files for clients that don't send Accept-Encoding: gzip, such as this
                // client, and leaves out Content-Encoding.
                if (!AmazonS3TestContext.IsGoogleCloudStorage)
                {
                    htmlMetadata.Headers.ContentEncoding.Should().Be("gzip");
                }

                var indexMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "index.json");
                indexMetadata.Headers.ContentType.Should().Be("application/json");

                await testContext.CleanupAsync();
            }
        }
    }
}
