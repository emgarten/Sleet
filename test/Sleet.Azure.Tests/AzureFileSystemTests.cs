using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
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
    }
}
