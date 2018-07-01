using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Test.Helpers;
using Sleet.Test.Common;
using Xunit;

namespace Sleet.CmdExe.Tests
{
    public class DynamicSettingsTests
    {
        [WindowsFact]
        public async Task InitWithEnvVarsVerifyFeedOutput()
        {
            using (var testContext = new SleetTestContext())
            {
                var dir = Path.Combine(testContext.Root, "sub");
                var args = $"init -c none";

                Directory.CreateDirectory(dir);

                var envVars = new Dictionary<string, string>()
                {
                    { "SLEET_FEED_TYPE", "local" },
                    { "SLEET_FEED_PATH", dir }
                };

                var result = await CmdRunner.RunAsync(ExeUtils.SleetExePath, dir, args, envVars);

                result.Success.Should().BeTrue();
                File.Exists(Path.Combine(dir, "sleet.settings.json")).Should().BeTrue();
            }
        }

        [WindowsFact]
        public async Task InitWithPropertiesVerifyFeedOutput()
        {
            using (var testContext = new SleetTestContext())
            {
                var dir = Path.Combine(testContext.Root, "sub");
                var args = $"init -p SLEET_FEED_TYPE=local -p \"SLEET_FEED_PATH={dir}\" -c none";

                Directory.CreateDirectory(dir);

                var result = await CmdRunner.RunAsync(ExeUtils.SleetExePath, dir, args);

                result.Success.Should().BeTrue();
                File.Exists(Path.Combine(dir, "sleet.settings.json")).Should().BeTrue();
            }
        }

        [WindowsFact]
        public async Task RunAllCommandsWithInputProperties()
        {
            using (var packagesFolder = new TestFolder())
            using (var testContext = new SleetTestContext())
            {
                var testPackage = new TestNupkg("packageA", "1.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);
                var dir = Path.Combine(testContext.Root, "sub");
                Directory.CreateDirectory(dir);

                var extraArgs = $" -p SLEET_FEED_TYPE=local -p \"SLEET_FEED_PATH={dir}\" -c none";
                var output = Path.Combine(testContext.Root, "out");
                Directory.CreateDirectory(output);

                var result = await CmdRunner.RunAsync(ExeUtils.SleetExePath, dir, "init" + extraArgs);
                result.Success.Should().BeTrue();

                result = await CmdRunner.RunAsync(ExeUtils.SleetExePath, dir, $"push {zipFile.FullName} " + extraArgs);
                result.Success.Should().BeTrue();

                result = await CmdRunner.RunAsync(ExeUtils.SleetExePath, dir, $"delete -i packageA " + extraArgs);
                result.Success.Should().BeTrue();

                result = await CmdRunner.RunAsync(ExeUtils.SleetExePath, dir, $"download -o {output} " + extraArgs);
                result.Success.Should().BeTrue();

                result = await CmdRunner.RunAsync(ExeUtils.SleetExePath, dir, $"stats" + extraArgs);
                result.Success.Should().BeTrue();

                result = await CmdRunner.RunAsync(ExeUtils.SleetExePath, dir, $"validate" + extraArgs);
                result.Success.Should().BeTrue();

                result = await CmdRunner.RunAsync(ExeUtils.SleetExePath, dir, $"recreate" + extraArgs);
                result.Success.Should().BeTrue();

                result = await CmdRunner.RunAsync(ExeUtils.SleetExePath, dir, $"feed-settings --get-all" + extraArgs);
                result.Success.Should().BeTrue();

                result = await CmdRunner.RunAsync(ExeUtils.SleetExePath, dir, $"destroy" + extraArgs);
                result.Success.Should().BeTrue();
            }
        }
    }
}
