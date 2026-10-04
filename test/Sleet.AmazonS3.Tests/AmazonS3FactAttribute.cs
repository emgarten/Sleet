using System.Runtime.CompilerServices;
using Xunit;

namespace Sleet.AmazonS3.Tests
{
    /// <summary>
    /// Runs the test against the Amazon S3 account in the SLEET_TEST_S3_* env vars. Skips the test if they aren't set.
    /// </summary>
    public sealed class AmazonS3FactAttribute
        : FactAttribute
    {
        public AmazonS3FactAttribute(
            [CallerFilePath] string sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            Skip = $"Set {AmazonS3TestContext.EnvAccessKeyId} and {AmazonS3TestContext.EnvSecretAccessKey} to run this test, see CONTRIBUTING.md.";
            SkipType = typeof(AmazonS3TestContext);
            SkipUnless = nameof(AmazonS3TestContext.IsAvailable);
        }
    }
}
