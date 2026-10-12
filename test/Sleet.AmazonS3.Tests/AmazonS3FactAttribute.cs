using System.Runtime.CompilerServices;
using Xunit;

namespace Sleet.AmazonS3.Tests
{
    /// <summary>
    /// Runs the test against the Amazon S3 account in the SLEET_TEST_S3_* env vars if they're set, otherwise against
    /// RustFS in local-env. Skips the test if neither is available.
    /// </summary>
    public sealed class AmazonS3FactAttribute
        : FactAttribute
    {
        public AmazonS3FactAttribute(
            [CallerFilePath] string sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            Skip = $"Start local-env or set {AmazonS3TestContext.EnvAccessKeyId} and {AmazonS3TestContext.EnvSecretAccessKey} to run this test, see CONTRIBUTING.md.";
            SkipType = typeof(AmazonS3TestContext);
            SkipUnless = nameof(AmazonS3TestContext.IsAvailable);
        }
    }
}
