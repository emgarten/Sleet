using System;
using Sleet.Test.Common;

namespace Sleet.CmdExe.Tests
{
    public static class ExeUtils
    {
        private static readonly Lazy<string> _getExe = new Lazy<string>(() => CmdRunner.GetPath("artifacts/publish/Sleet.exe"));

        public static string SleetExePath => _getExe.Value;
    }
}
