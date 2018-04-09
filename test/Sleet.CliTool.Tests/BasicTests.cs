using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using Sleet.Test.Common;
using Xunit;

namespace Sleet.CliTool.Tests
{
    public class BasicTests
    {
        /// <summary>
        /// Dotnet install sleet
        /// 
        /// Currently the sleet nupkg is only produced on Windows,
        /// for that reason this test only runs on windows.
        /// </summary>
        [Fact]
        public async Task InstallToolVerifySuccess()
        {
            using (var testContext = new SleetTestContext())
            {
                var dir = Path.Combine(testContext.Root, "tooloutput");
                Directory.CreateDirectory(dir);

                var dotnetExe = GetDotnetPath();
                var exeFile = new FileInfo(dotnetExe);
                var nupkgsFolder = Path.Combine(exeFile.Directory.Parent.FullName, "artifacts", "nupkgs");

                var packages = LocalFolderUtility.GetPackagesV2(nupkgsFolder, "Sleet", NullLogger.Instance).ToList();

                if (packages.Count < 1)
                {
                    throw new Exception("Run build.ps1 first to create the nupkgs.");
                }

                var sleetNupkg = packages
                    .OrderByDescending(e => e.Nuspec.GetVersion())
                    .First();

                var sleetVersion = sleetNupkg.Nuspec.GetVersion().ToNormalizedString();

                var result = await CmdRunner.RunAsync(dotnetExe, testContext.Root, $"tool install sleet --version {sleetVersion} --source-feed {nupkgsFolder} --tool-path {dir}");
                result.Success.Should().BeTrue(result.AllOutput);

                var sleetDllPath = Path.Combine(dir, ".store", "sleet", sleetVersion, "sleet", sleetVersion, "tools", "netcoreapp2.0", "any", "Sleet.dll");

                if (!File.Exists(sleetDllPath))
                {
                    throw new Exception("Tool did not install to the expected location: " + sleetDllPath);
                }

                // Run the tool

                result = await CmdRunner.RunAsync(dotnetExe, dir, $"{sleetDllPath} createconfig");
                result.Success.Should().BeTrue(result.AllOutput);

                File.Exists(Path.Combine(dir, "sleet.json")).Should().BeTrue("sleet should have generated the config file");
            }
        }

        private static string GetDotnetPath()
        {
            var dotnetExeRelativePath = ".cli/dotnet.exe";

            if (!RuntimeEnvironmentHelper.IsWindows)
            {
                dotnetExeRelativePath = ".cli/dotnet";
            }

            return CmdRunner.GetPath(dotnetExeRelativePath);
        }

        private static void Delete(DirectoryInfo dir)
        {
            if (!dir.Exists)
            {
                return;
            }

            try
            {
                foreach (var subDir in dir.EnumerateDirectories())
                {

                    Delete(subDir);
                }

                dir.Delete(true);
            }
            catch
            {
                // Ignore exceptions
            }
        }
    }
}
