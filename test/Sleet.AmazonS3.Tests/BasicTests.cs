using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using FluentAssertions;
using NuGet.Test.Helpers;
using Sleet.Test.Common;

namespace Sleet.AmazonS3.Tests
{
    public class BasicTests
    {
        [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task GivenAStorageAccountVerifyInitSucceeds()
        {
            using (var testContext = new AmazonS3TestContext())
            {
                await testContext.InitAsync();

                var result = await InitCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    enableCatalog: true,
                    enableSymbols: true,
                    log: testContext.Logger,
                    token: CancellationToken.None);

                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    testContext.Logger);

                result.Should().BeTrue();

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task GivenAStorageAccountVerifyPushSucceeds()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AmazonS3TestContext())
            {
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

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task GivenAStorageAccountWithNoContainerVerifyPushSucceeds()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AmazonS3TestContext())
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

                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    testContext.Logger);

                result.Should().BeTrue();

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task GivenAStorageAccountWithNoContainerPublicAclVerifyPushSucceeds()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AmazonS3TestContext(acl: "public-read"))
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

                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    testContext.Logger);

                result.Should().BeTrue();

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task GivenAStorageAccountWithNoInitVerifyPushSucceeds()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AmazonS3TestContext())
            {
                await testContext.InitAsync();

                var testPackage = new TestNupkg("packageA", "1.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);

                // Skip init
                var result = await PushCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    new List<string>() { zipFile.FullName },
                    force: false,
                    skipExisting: false,
                    log: testContext.Logger);

                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    testContext.Logger);

                result.Should().BeTrue();

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task GivenAStorageAccountVerifyPushAndRemoveSucceed()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AmazonS3TestContext())
            {
                await testContext.InitAsync();

                var testPackage = new TestNupkg()
                {
                    Nuspec = new TestNuspec()
                    {
                        Id = "packageA",
                        Version = "1.0.0",
                        Authors = "author",
                        Description = "desc",
                        IconUrl = "http://www.tempuri.org",
                        Icon = "images/icon.png",
                        Readme = "README.md",
                        Language = "en-us",
                        MinClientVersion = "1.0.0",
                        Title = "title",
                        Tags = "a b d",
                        Summary = "summary",
                        LicenseUrl = "http://www.tempuri.org/lic",
                        ProjectUrl = "http://www.tempuri.org/proj",
                        ReleaseNotes = "notes",
                        Owners = "owners",
                        Copyright = "copyright",
                        RequireLicenseAcceptance = "true"
                    },
                    Files = new List<TestNupkgFile>()
                    {
                        new("README.md"),
                        new("images/icon.png")
                    }
                };

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

                result &= await DeleteCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    "packageA",
                    "1.0.0",
                    "test",
                    force: true,
                    log: testContext.Logger);

                result &= await ValidateCommand.RunAsync(testContext.LocalSettings,
                    testContext.FileSystem,
                    testContext.Logger);

                result.Should().BeTrue();

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task GivenAStorageAccountVerifyPushAndSucceedWithBaseURI()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new AmazonS3TestContext())
            {
                var baseUri = new Uri("http://tempuri.org/abc/");
                var fileSystem = new AmazonS3FileSystem(
                        testContext.LocalCache, testContext.Uri, baseUri,
                        testContext.Client, testContext.BucketName, ServerSideEncryptionMethod.None
                    );

                testContext.FileSystem = fileSystem;

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

                // Check baseURIs
                await BaseURITestUtil.VerifyBaseUris(testContext.FileSystem.Files.Values, baseUri);

                await testContext.CleanupAsync();
            }
        }
    }
}