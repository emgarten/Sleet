using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Test.Helpers;
using Sleet.Test.Common;

namespace Sleet.MinioS3.Tests
{
    public class MinioS3FileSystemTests
    {
        [EnvVarExistsFact(MinioS3TestContext.EnvAccessKeyId)]
        public async Task GivenAS3AccountVerifyBucketOperations()
        {
            using (var testContext = new MinioS3TestContext())
            {
                testContext.CreateBucketOnInit = false;
                await testContext.InitAsync();

                // Verify at the start
                (await testContext.FileSystem.HasBucket(testContext.Logger, CancellationToken.None)).Should().BeFalse("HasBucket should return false since it does not exist yet");
                (await testContext.FileSystem.Validate(testContext.Logger, CancellationToken.None)).Should().BeFalse("Validate should be false since the bucket does not exist yet");

                // Create
                await testContext.FileSystem.CreateBucket(testContext.Logger, CancellationToken.None);

                var hasBucket = (await testContext.FileSystem.HasBucket(testContext.Logger, CancellationToken.None));
                hasBucket.Should().BeTrue("HasBucket should return true after");
                var valid = (await testContext.FileSystem.Validate(testContext.Logger, CancellationToken.None));
                valid.Should().BeTrue("Validate should be true after");

                // Delete
                await testContext.FileSystem.DeleteBucket(testContext.Logger, CancellationToken.None);

                (await testContext.FileSystem.HasBucket(testContext.Logger, CancellationToken.None)).Should().BeFalse();
                (await testContext.FileSystem.Validate(testContext.Logger, CancellationToken.None)).Should().BeFalse();

                await testContext.CleanupAsync();
            }
        }

    }
}