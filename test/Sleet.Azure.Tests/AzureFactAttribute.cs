using System.Runtime.CompilerServices;
using Xunit;

namespace Sleet.Azure.Tests
{
    /// <summary>
    /// Runs the test against SLEET_TEST_ACCOUNT if it's set, otherwise against Azurite in local-env.
    /// Skips the test if neither is available.
    /// </summary>
    public sealed class AzureFactAttribute
        : FactAttribute
    {
        public AzureFactAttribute(
            [CallerFilePath] string sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            Skip = $"Start local-env or set {AzureTestContext.EnvVarName} to run this test, see CONTRIBUTING.md.";
            SkipType = typeof(AzureTestContext);
            SkipUnless = nameof(AzureTestContext.IsAvailable);
        }
    }
}
