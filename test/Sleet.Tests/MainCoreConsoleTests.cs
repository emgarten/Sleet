using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using NuGet.Common;
using NuGet.Test.Helpers;
using Xunit;

namespace Sleet.Tests
{
    [Collection(NonParallelCollection.Name)]
    public class MainCoreConsoleTests
    {
        [Theory]
        [InlineData("unknown-command")]
        [InlineData("init --unknown")]
        public async Task MainCore_ParseErrors_ReturnOneAndWriteHelp(string arguments)
        {
            using (var writer = new StringWriter())
            {
                var originalOut = Console.Out;
                Console.SetOut(writer);
                try
                {
                    using (var log = new ConsoleLogger())
                    {
                        var exitCode = await Program.MainCore(arguments.Split(' '), log);

                        exitCode.Should().Be(1);
                    }

                    writer.ToString().Should().Contain("Usage:");
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }

        [Fact]
        public async Task MainCore_HelpAndVersion_WriteToConsole()
        {
            var help = await RunWithConsoleAsync("--help");
            help.ExitCode.Should().Be(0);
            help.Output.Should().Contain("Usage:");
            help.Output.Should().Contain("createconfig");

            var version = await RunWithConsoleAsync("--version");
            version.ExitCode.Should().Be(0);
            version.Output.Should().NotBeNullOrWhiteSpace();
        }

        [Theory]
        [InlineData(null, null, false, LogLevel.Information, true)]
        [InlineData(null, null, true, LogLevel.Verbose, false)]
        [InlineData(null, "quiet", true, LogLevel.Warning, true)]
        [InlineData("1", "quiet", false, LogLevel.Debug, false)]
        public async Task MainCore_SetVerbosity_UsesExpectedPrecedence(string debugValue, string verbosity, bool verbose, LogLevel expectedLevel, bool expectedCollapseMessages)
        {
            using (var root = new TestFolder())
            using (var writer = new StringWriter())
            {
                var oldDebug = Environment.GetEnvironmentVariable("SLEET_DEBUG");
                var originalOut = Console.Out;
                Console.SetOut(writer);
                try
                {
                    Environment.SetEnvironmentVariable("SLEET_DEBUG", debugValue);
                    using (var log = new ConsoleLogger(LogLevel.Error))
                    {
                        var output = Path.Combine(root.Root, Guid.NewGuid().ToString());
                        Directory.CreateDirectory(output);
                        var args = new System.Collections.Generic.List<string>() { "createconfig", "--local", "--output", output };
                        if (verbose)
                        {
                            args.Add("--verbose");
                        }
                        if (verbosity != null)
                        {
                            args.Add("--verbosity");
                            args.Add(verbosity);
                        }

                        var exitCode = await Program.MainCore(args.ToArray(), log);

                        exitCode.Should().Be(0);
                        log.VerbosityLevel.Should().Be(expectedLevel);
                        log.CollapseMessages.Should().Be(expectedCollapseMessages);
                    }
                }
                finally
                {
                    Environment.SetEnvironmentVariable("SLEET_DEBUG", oldDebug);
                    Console.SetOut(originalOut);
                }
            }
        }

        [Theory]
        [InlineData("loud")]
        [InlineData("")]
        public async Task MainCore_InvalidVerbosity_ReturnsOneAndLogsFormattedError(string verbosity)
        {
            using (var root = new TestFolder())
            using (var writer = new StringWriter())
            {
                var originalOut = Console.Out;
                Console.SetOut(writer);
                try
                {
                    using (var log = new ConsoleLogger())
                    {
                        var exitCode = await Program.MainCore(new[] { "createconfig", "--local", "--output", root.Root, "--verbosity", verbosity }, log);

                        exitCode.Should().Be(1);
                    }

                    writer.ToString().Should().Contain($"Invalid verbosity: '{verbosity}'");
                    writer.ToString().Should().Contain("[System.ArgumentException]");
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }

        private static async Task<(int ExitCode, string Output)> RunWithConsoleAsync(params string[] args)
        {
            using (var writer = new StringWriter())
            {
                var originalOut = Console.Out;
                Console.SetOut(writer);
                try
                {
                    using (var log = new ConsoleLogger())
                    {
                        var exitCode = await Program.MainCore(args, log);
                        return (exitCode, writer.ToString());
                    }
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }
    }
}
