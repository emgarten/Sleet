using FluentAssertions;
using NuGet.Test.Helpers;
using Sleet.Test.Common;

namespace Sleet.Azure.Tests
{
    public class ContentTypeTests
    {
        [EnvVarExistsFact(AzureTestContext.EnvVarName)]
        public async Task GivenInitVerifyGalleryContentTypeIsHtml()
        {
            using (var testContext = new AzureTestContext())
            {
                await testContext.InitAsync();

                // The browser must receive text/html to render the gallery
                // instead of downloading it.
                var htmlBlob = testContext.Container.GetBlobClient("index.html");
                var htmlProperties = await htmlBlob.GetPropertiesAsync();
                htmlProperties.Value.ContentType.Should().Be("text/html; charset=utf-8");

                var indexBlob = testContext.Container.GetBlobClient("index.json");
                var indexProperties = await indexBlob.GetPropertiesAsync();
                indexProperties.Value.ContentType.Should().Be("application/json");

                await testContext.CleanupAsync();
            }
        }
    }
}
