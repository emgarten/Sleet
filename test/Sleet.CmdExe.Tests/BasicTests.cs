using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Sleet.Test.Common;
using Xunit;

namespace Sleet.CmdExe.Tests
{
    public class BasicTests
    {
        [Theory]
        [InlineData("foo")]
        [InlineData("init --foo")]
        [InlineData("init")]
        public async Task GivenABadCommandVerifyFailure(string arguments)
        {
            using (var testContext = new SleetTestContext())
            {
                var result = await CmdRunner.RunAsync(ExeUtils.SleetExePath, testContext.Root, arguments);

                result.Success.Should().BeFalse();
            }
        }

        [Fact]
        public async Task RunCreateConfigVerifySuccess()
        {
            using (var testContext = new SleetTestContext())
            {
                var args = "createconfig";

                var dir = Path.Combine(testContext.Root, "sub");
                Directory.CreateDirectory(dir);

                var result = await CmdRunner.RunAsync(ExeUtils.SleetExePath, dir, args);

                result.Success.Should().BeTrue();
                File.Exists(Path.Combine(dir, "sleet.json")).Should().BeTrue();
            }
        }
    }
}
