using System;
using System.IO;
using NuGet.Common;
using Xunit;

namespace Sleet.Test.Common
{
    public sealed class WindowsFactAttribute
        : FactAttribute
    {
        public override string Skip => RuntimeEnvironmentHelper.IsWindows ? null : "Windows only test";
    }

    public sealed class WindowsTheoryAttribute
    : TheoryAttribute
    {
        public override string Skip => RuntimeEnvironmentHelper.IsWindows ? null : "Windows only test";
    }

    public sealed class FileExistsAttribute
        : FactAttribute
    {
        private readonly string _path;
        private readonly string _envVarToPath;

        public FileExistsAttribute(string envVarToPath)
        {
            _path = Environment.GetEnvironmentVariable(envVarToPath);
            _envVarToPath = envVarToPath;
        }

        public override string Skip => (!string.IsNullOrEmpty(_path) && File.Exists(_path)) ? null : $"Set EnvVar: {_envVarToPath} to a valid path to run this test.";
    }
}
