#if TEST_AMAZON_S3
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Test.Helpers;
using Sleet.Test.Common;

namespace Sleet.AmazonS3.Tests
{
    public class SubFeedTests
    {
        [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task SubFeed_InitMultipleFeedsVerifyDestroyDoesNotModifyOthers()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AmazonS3TestContext())
            using (var testContext2 = new AmazonS3TestContext())
            {
                // Use a subfeed for the filesystem
                var subFeedName = "testSubFeedA";
                var subFeedName2 = "testSubFeedB";
                var root = UriUtility.GetPath(testContext.Uri, subFeedName);
                var root2 = UriUtility.GetPath(testContext.Uri, subFeedName2);
                testContext.FileSystem = new AmazonS3FileSystem(testContext.LocalCache, root, root, testContext.Client, testContext.BucketName, feedSubPath: subFeedName);
                testContext2.FileSystem = new AmazonS3FileSystem(testContext.LocalCache, root2, root2, testContext.Client, testContext.BucketName, feedSubPath: subFeedName2);

                await testContext.InitAsync();
                await testContext2.InitAsync();

                var testPackage = new TestNupkg("packageA", "1.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);

                var result = await InitCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.Logger,
                    token: CancellationToken.None);

                result &= await InitCommand.RunAsync(testContext.LocalSettings,
                    testContext2.FileSystem,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext2.Logger,
                    token: CancellationToken.None);

                // Destroy feed2
                result &= await DestroyCommand.RunAsync(testContext.LocalSettings,
                    testContext2.FileSystem,
                    testContext2.Logger);

                // Validate feed1
                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    testContext.Logger);


                result.Should().BeTrue();

                await testContext.CleanupAsync();
                await testContext2.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task SubFeed_PushAndVerifyNoFilesInRoot()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AmazonS3TestContext())
            {
                // Use a subfeed for the filesystem
                var subFeedName = "testSubFeed";
                var root = UriUtility.GetPath(testContext.Uri, subFeedName);
                testContext.FileSystem = new AmazonS3FileSystem(testContext.LocalCache, root, root, testContext.Client, testContext.BucketName, feedSubPath: subFeedName);

                await testContext.InitAsync();

                var testPackage = new TestNupkg("packageA", "1.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);

                var result = await InitCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.Logger,
                    token: CancellationToken.None);

                result &= await PushCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    new List<string>() { zipFile.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.Logger);

                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    testContext.Logger);

                result.Should().BeTrue();

                var files = await AmazonS3FileSystemAbstraction.GetFilesAsync(testContext.Client, testContext.BucketName, CancellationToken.None);

                files.Where(e => e.Key.IndexOf(subFeedName, StringComparison.OrdinalIgnoreCase) < 0).Should().BeEmpty();
                files.Where(e => e.Key.IndexOf(subFeedName, StringComparison.OrdinalIgnoreCase) > -1).Should().NotBeEmpty();

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task SubFeed_PushAndVerifyWithNestedFeedsVerifySuccess()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AmazonS3TestContext())
            using (var testContext2 = new AmazonS3TestContext())
            {
                // Use a subfeed for the filesystem
                var subFeedName = "testSubFeed";
                var root = UriUtility.GetPath(testContext.Uri, subFeedName);
                testContext.FileSystem = new AmazonS3FileSystem(testContext.LocalCache, root, root, testContext.Client, testContext.BucketName, feedSubPath: subFeedName);

                await testContext.InitAsync();
                await testContext2.InitAsync();

                var testPackage = new TestNupkg("packageA", "1.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);

                var result = await InitCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.Logger,
                    token: CancellationToken.None);

                result &= await InitCommand.RunAsync(testContext.LocalSettings,
                    testContext2.FileSystem,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext2.Logger,
                    token: CancellationToken.None);

                result &= await PushCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    new List<string>() { zipFile.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.Logger);

                result &= await PushCommand.RunAsync(testContext.LocalSettings,
                    testContext2.FileSystem,
                    new List<string>() { zipFile.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext2.Logger);

                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    testContext.Logger);

                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext2.FileSystem,
                    testContext2.Logger);

                result.Should().BeTrue();

                await testContext.CleanupAsync();
                await testContext2.CleanupAsync();
            }
        }
    }
}
#endif