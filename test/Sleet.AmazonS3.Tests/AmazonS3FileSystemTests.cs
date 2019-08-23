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
    public class AmazonS3FileSystemTests
    {
        // Disabled due to timing issues that cause this to fail on the CI
        // [EnvVarExistsFact(AmazonS3TestContext.EnvAccessKeyId)]
        public async Task GivenAS3AccountVerifyBucketOperations()
        {
            using (var testContext = new AmazonS3TestContext())
            {
                testContext.CreateBucketOnInit = false;
                await testContext.InitAsync();

                // Verify at the start
                (await testContext.FileSystem.HasBucket(testContext.Logger, CancellationToken.None)).Should().BeFalse();
                (await testContext.FileSystem.Validate(testContext.Logger, CancellationToken.None)).Should().BeFalse();

                // Create
                await testContext.FileSystem.CreateBucket(testContext.Logger, CancellationToken.None);

                (await testContext.FileSystem.HasBucket(testContext.Logger, CancellationToken.None)).Should().BeTrue();
                (await testContext.FileSystem.Validate(testContext.Logger, CancellationToken.None)).Should().BeTrue();

                await testContext.CleanupAsync();
            }
        }
    }
}