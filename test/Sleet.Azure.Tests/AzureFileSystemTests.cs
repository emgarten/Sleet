using FluentAssertions;
using Newtonsoft.Json.Linq;
using Sleet.Test.Common;

namespace Sleet.Azure.Tests
{
    /// <summary>
    /// These tests can run locally against developer storage by changing
    /// EnvVarExistsFactAttribute -> Fact and starting up the emulator.
    /// </summary>
    public class AzureFileSystemTests
    {
        [EnvVarExistsFact(AzureTestContext.EnvVarName)]
        public async Task GivenAStorageAccountVerifyContainerOperations()
        {
            using (var testContext = new AzureTestContext())
            {
                testContext.CreateContainerOnInit = false;
                await testContext.InitAsync();

                // Verify at the start
                (await testContext.FileSystem.HasBucket(testContext.Logger, CancellationToken.None)).Should().BeFalse();
                (await testContext.FileSystem.Validate(testContext.Logger, CancellationToken.None)).Should().BeFalse();

                // Create
                await testContext.FileSystem.CreateBucket(testContext.Logger, CancellationToken.None);

                (await testContext.FileSystem.HasBucket(testContext.Logger, CancellationToken.None)).Should().BeTrue();
                (await testContext.FileSystem.Validate(testContext.Logger, CancellationToken.None)).Should().BeTrue();

                // Delete
                await testContext.FileSystem.DeleteBucket(testContext.Logger, CancellationToken.None);

                (await testContext.FileSystem.HasBucket(testContext.Logger, CancellationToken.None)).Should().BeFalse();
                (await testContext.FileSystem.Validate(testContext.Logger, CancellationToken.None)).Should().BeFalse();

                await testContext.CleanupAsync();
            }
        }

        [EnvVarExistsFact(AzureTestContext.EnvVarName)]
        public async Task GivenAStorageAccountConnStringVerifyFileSystemFactoryCreatesFS()
        {
            using (var testContext = new AzureTestContext())
            {
                testContext.CreateContainerOnInit = false;
                await testContext.InitAsync();

                var settings = LocalSettings.Load(new JObject(
                    new JProperty("sources",
                        new JArray(
                            new JObject(
                                new JProperty("name", "azure"),
                                new JProperty("type", "azure"),
                                new JProperty("container", testContext.ContainerName),
                                new JProperty("connectionString", AzureTestContext.GetConnectionString()))))));

                var fs = await FileSystemFactory.CreateFileSystemAsync(settings, testContext.LocalCache, "azure", testContext.Logger);
                fs.GetPath("test.txt").AbsolutePath.Should().Contain("/test.txt");

                // Verify at the start
                (await fs.HasBucket(testContext.Logger, CancellationToken.None)).Should().BeFalse();
                (await fs.Validate(testContext.Logger, CancellationToken.None)).Should().BeFalse();

                // Create
                await fs.CreateBucket(testContext.Logger, CancellationToken.None);

                (await fs.HasBucket(testContext.Logger, CancellationToken.None)).Should().BeTrue();
                (await fs.Validate(testContext.Logger, CancellationToken.None)).Should().BeTrue();

                // Delete
                await fs.DeleteBucket(testContext.Logger, CancellationToken.None);

                (await fs.HasBucket(testContext.Logger, CancellationToken.None)).Should().BeFalse();
                (await fs.Validate(testContext.Logger, CancellationToken.None)).Should().BeFalse();

                await testContext.CleanupAsync();
            }
        }
    }
}
