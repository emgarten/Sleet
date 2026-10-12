using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Helpers;
using Xunit;

namespace Sleet.AmazonS3.Tests
{
    public class AmazonS3NuGetIntegrationTests
    {
        [AmazonS3Fact]
        public async Task GivenPushCreatesAnS3BucketVerifyNuGetCanRead()
        {
            Assert.SkipWhen(AmazonS3TestContext.IsCloudflareR2, "Public access for Cloudflare R2 is set up outside the S3 API, with a custom domain or r2.dev URL, so NuGet can't read the bucket this test creates.");

            using (var packagesFolder = new TestFolder())
            await using (var testContext = new AmazonS3TestContext())
            using (var sourceContext = new SourceCacheContext())
            {
                // Skip creation and allow it to be done during push.
                testContext.CreateBucketOnInit = false;
                await testContext.InitAsync();

                var testPackage = new TestNupkg("packageA", "1.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);

                var result = await PushCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    new List<string>() { zipFile.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.Logger);

                // Read the feed with NuGet.Protocol
                var feedIndex = $"{testContext.FileSystem.Root.AbsoluteUri}index.json";
                var repo = Repository.Factory.GetCoreV3(feedIndex);
                var resource = await repo.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None);

                var packageResults = (await resource.GetAllVersionsAsync("packageA", sourceContext, NullLogger.Instance, CancellationToken.None)).ToList();
                packageResults.Count.Should().Be(1);
                packageResults[0].ToIdentityString().Should().Be("1.0.0");

                await testContext.CleanupAsync();
            }
        }
    }
}