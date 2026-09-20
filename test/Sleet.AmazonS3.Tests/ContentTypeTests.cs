using FluentAssertions;
using NuGet.Test.Helpers;
using Sleet.Test.Common;

namespace Sleet.AmazonS3.Tests
{
    public class ContentTypeTests
    {
        [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task GivenInitVerifyFeedUIContentTypeIsHtml()
        {
            using (var testContext = new AmazonS3TestContext())
            {
                await testContext.InitAsync();

                // The browser must receive text/html to render the feed UI
                // instead of downloading it.
                var htmlMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "index.html");
                htmlMetadata.Headers.ContentType.Should().Be("text/html; charset=utf-8");

                var indexMetadata = await testContext.Client.GetObjectMetadataAsync(
                    testContext.BucketName,
                    "index.json");
                indexMetadata.Headers.ContentType.Should().Be("application/json");

                await testContext.CleanupAsync();
            }
        }
    }
}
