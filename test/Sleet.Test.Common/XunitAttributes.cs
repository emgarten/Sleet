using System;
using System.Runtime.CompilerServices;
using NuGet.Common;
using Xunit;

namespace Sleet.Test.Common
{
    public sealed class WindowsFactAttribute
        : FactAttribute
    {
        public WindowsFactAttribute(
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            if (!RuntimeEnvironmentHelper.IsWindows)
            {
                Skip = "Windows only test";
            }
        }
    }

    public sealed class WindowsTheoryAttribute
        : TheoryAttribute
    {
        public WindowsTheoryAttribute(
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            if (!RuntimeEnvironmentHelper.IsWindows)
            {
                Skip = "Windows only test";
            }
        }
    }

    public sealed class EnvVarExistsFactAttribute
        : FactAttribute
    {
        public EnvVarExistsFactAttribute(
            string envVar,
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar)))
            {
                Skip = $"Set env var: {envVar} to run this test. This can be ignored for non CI scenarios.";
            }
        }
    }
}