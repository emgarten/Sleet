using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Test.Helpers;
using Sleet;
using Sleet.Test.Common;
using Xunit;

namespace SleetLib.Tests
{
    public class ValidateCommandTests
    {
        [Fact]
        public async Task ValidateCommand_WithValidFeedReturnsTrueWithoutErrors()
        {
            using (var testContext = await CreateBasicFeedAsync())
            {
                var log = new TestLogger();

                var result = await ValidateCommand.RunAsync(testContext.SleetContext.LocalSettings, testContext.SleetContext.Source, log);

                result.Should().BeTrue();
                log.GetMessages(LogLevel.Error).Should().BeEmpty();
                log.GetMessages().Should().Contain("Feed valid");
            }
        }

        [Fact]
        public async Task ValidateCommand_WithAutocompleteMismatchReturnsFalse()
        {
            using (var testContext = await CreateBasicFeedAsync())
            {
                WriteJson(testContext.Target, "autocomplete/query", new JObject
                {
                    { "@context", new JObject() },
                    { "totalHits", 0 },
                    { "data", new JArray() }
                });

                var log = new TestLogger();
                var result = await ValidateCommand.RunAsync(testContext.SleetContext.LocalSettings, testContext.SleetContext.Source, log);

                result.Should().BeFalse();
                log.GetMessages().Should().Contain("Missing autocomplete packages: a");
                log.GetMessages().Should().Contain("Feed invalid!");
            }
        }

        [Fact]
        public async Task ValidateCommand_WithRegistrationMismatchReturnsFalse()
        {
            using (var testContext = await CreateBasicFeedAsync())
            {
                File.Delete(Path.Combine(testContext.Target, "registration", "a", "index.json"));

                var log = new TestLogger();
                var result = await ValidateCommand.RunAsync(testContext.SleetContext.LocalSettings, testContext.SleetContext.Source, log);

                result.Should().BeFalse();
                log.GetMessages().Should().Contain("Validating Registrations");
                AssertPackageMissing(log, "a", "1.0.0");
                log.GetMessages().Should().Contain("Feed invalid!");
            }
        }

        [Fact]
        public async Task ValidateCommand_WithFlatContainerMismatchReturnsFalse()
        {
            using (var testContext = await CreateBasicFeedAsync())
            {
                File.Delete(Path.Combine(testContext.Target, "flatcontainer", "a", "index.json"));

                var log = new TestLogger();
                var result = await ValidateCommand.RunAsync(testContext.SleetContext.LocalSettings, testContext.SleetContext.Source, log);

                result.Should().BeFalse();
                log.GetMessages().Should().Contain("Validating FlatContainer");
                AssertPackageMissing(log, "a", "1.0.0");
                log.GetMessages().Should().Contain("Feed invalid!");
            }
        }

        [Fact]
        public async Task ValidateCommand_WithSearchMismatchReturnsFalse()
        {
            using (var testContext = await CreateBasicFeedAsync())
            {
                WriteJson(testContext.Target, "search/query", new JObject
                {
                    { "@context", new JObject() },
                    { "totalHits", 0 },
                    { "data", new JArray() }
                });

                var log = new TestLogger();
                var result = await ValidateCommand.RunAsync(testContext.SleetContext.LocalSettings, testContext.SleetContext.Source, log);

                result.Should().BeFalse();
                log.GetMessages().Should().Contain("Validating Search");
                AssertPackageMissing(log, "a", "1.0.0");
                log.GetMessages().Should().Contain("Feed invalid!");
            }
        }

        [Fact]
        public async Task ValidateCommand_WithPackageIndexMismatchReturnsFalse()
        {
            using (var testContext = await CreateBasicFeedAsync())
            {
                var packageIndexPath = Path.Combine(testContext.Target, "sleet.packageindex.json");
                var json = JObject.Parse(File.ReadAllText(packageIndexPath));
                var packages = (JObject)json["packages"];
                packages.Add("ghost", new JArray("9.9.9"));
                WriteJson(packageIndexPath, json);

                var log = new TestLogger();
                var result = await ValidateCommand.RunAsync(testContext.SleetContext.LocalSettings, testContext.SleetContext.Source, log);

                result.Should().BeFalse();
                log.GetMessages().Should().Contain("Missing autocomplete packages: ghost");
                AssertPackageMissing(log, "ghost", "9.9.9");
                log.GetMessages().Should().Contain("Feed invalid!");
            }
        }

        private static async Task<SleetTestContext> CreateBasicFeedAsync()
        {
            var testContext = new SleetTestContext();
            var context = testContext.SleetContext;
            context.Token = TestContext.Current.CancellationToken;
            context.SourceSettings.CatalogEnabled = true;

            var testPackage = new TestNupkg("a", "1.0.0");
            var zipFile = testPackage.Save(testContext.Packages);

            await InitCommand.InitAsync(context);
            var pushResult = await PushCommand.RunAsync(context.LocalSettings, context.Source, new List<string>() { zipFile.FullName }, false, false, context.Log);
            pushResult.Should().BeTrue(((TestLogger)context.Log).GetMessages(LogLevel.Error));

            var validateResult = await ValidateCommand.RunAsync(context.LocalSettings, context.Source, context.Log);
            validateResult.Should().BeTrue(((TestLogger)context.Log).GetMessages(LogLevel.Error));

            return testContext;
        }

        private static void AssertPackageMissing(TestLogger log, string id, string version)
        {
            log.GetMessages().Should().Contain("Missing packages: 1");
            log.GetMessages().Should().Contain($"  {id} {version}");
        }

        private static void WriteJson(string root, string relativePath, JObject json)
        {
            WriteJson(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)), json);
        }

        private static void WriteJson(string path, JObject json)
        {
            var text = json.ToString(Formatting.Indented).Replace("\r\n", "\n");
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }
}
