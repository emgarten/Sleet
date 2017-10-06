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
        /// Add a DotNetCliToolReference to sleet
        /// Restore the project
        /// Run dotnet sleet to verify the tool is working
        /// </summary>
        [Fact]
        public async Task RunToolWithCreateConfigVerifySuccess()
        {
            using (var testContext = new SleetTestContext())
            {
                var dir = Path.Combine(testContext.Root, "project");
                Directory.CreateDirectory(dir);

                var dotnetExe = GetDotnetPath();
                var exeFile = new FileInfo(dotnetExe);
                var nupkgsFolder = Path.Combine(exeFile.Directory.Parent.FullName, "artifacts", "nupkgs");

                var sleetNupkg = LocalFolderUtility.GetPackagesV2(nupkgsFolder, "Sleet", NullLogger.Instance)
                    .OrderByDescending(e => e.Nuspec.GetVersion())
                    .First();

                var sleetVersion = sleetNupkg.Nuspec.GetVersion().ToNormalizedString();

                var result = await CmdRunner.RunAsync(dotnetExe, dir, "new classlib");
                result.Success.Should().BeTrue();

                var projectPath = Path.Combine(dir, "project.csproj");

                var pathContext = NuGetPathContext.Create(dir);
                var pathResolver = new FallbackPackagePathResolver(pathContext);

                // Delete restore assets file
                var toolInstallPath = Path.Combine(pathContext.UserPackageFolder, ".tools", "sleet");
                Delete(new DirectoryInfo(toolInstallPath));

                // Delete the tool package itself if it exists
                var toolPackagePath = Path.Combine(pathContext.UserPackageFolder, "sleet", sleetVersion);
                Delete(new DirectoryInfo(toolPackagePath));

                // Add a reference to the tool
                var xml = XDocument.Load(projectPath);
                xml.Root.Add(new XElement(XName.Get("ItemGroup"),
                    new XElement(XName.Get("DotNetCliToolReference"),
                    new XAttribute("Include", "Sleet"),
                    new XAttribute("Version", sleetVersion))));
                xml.Save(projectPath);

                // Restore the tool
                result = await CmdRunner.RunAsync(dotnetExe, dir, $"restore --source {nupkgsFolder}");
                result.Success.Should().BeTrue();

                // Run the tool
                result = await CmdRunner.RunAsync(dotnetExe, dir, $"sleet createconfig");
                result.Success.Should().BeTrue();

                File.Exists(Path.Combine(dir, "sleet.json")).Should().BeTrue();
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
