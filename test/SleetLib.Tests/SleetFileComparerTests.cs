using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class SleetFileComparerTests
    {
        [Fact]
        public void Compare_WithNulls_OrdersNullBeforeFiles()
        {
            var file = new PureSleetFile("https://example/index.json");
            var comparer = new SleetFileComparer();

            comparer.Compare(null, null).Should().Be(0);
            comparer.Compare(null, file).Should().BeNegative();
            comparer.Compare(file, null).Should().BePositive();
        }

        [Fact]
        public void Compare_OrdersIndexBeforeSearchQuery()
        {
            var index = new PureSleetFile("https://example/index.json");
            var query = new PureSleetFile("https://example/query");
            var comparer = new SleetFileComparer();

            comparer.Compare(index, query).Should().BeNegative();
            comparer.Compare(query, index).Should().BePositive();
        }

        [Fact]
        public void Compare_WithSamePriority_FallsBackToCaseInsensitiveUriOrder()
        {
            var a = new PureSleetFile("https://example/a.json");
            var b = new PureSleetFile("https://example/B.json");
            var upper = new PureSleetFile("https://example/A.json");
            var comparer = new SleetFileComparer();

            comparer.Compare(a, b).Should().BeNegative();
            comparer.Compare(a, upper).Should().Be(0);
        }

        [Fact]
        public void Compare_OrdersFilesByUploadPriority()
        {
            var files = new List<ISleetFile>()
            {
                new PureSleetFile("https://example/search/query"),
                new PureSleetFile("https://example/registration/a/index.json"),
                new PureSleetFile("https://example/catalog/data/a.json"),
                new PureSleetFile("https://example/flatcontainer/a/1.0.0/a.nuspec"),
                new PureSleetFile("https://example/flatcontainer/a/1.0.0/a.1.0.0.nupkg")
            };

            files.Sort(new SleetFileComparer());

            files.Select(e => e.EntityUri.AbsoluteUri).Should().Equal(
                "https://example/flatcontainer/a/1.0.0/a.1.0.0.nupkg",
                "https://example/flatcontainer/a/1.0.0/a.nuspec",
                "https://example/catalog/data/a.json",
                "https://example/registration/a/index.json",
                "https://example/search/query");
        }

        [Theory]
        [InlineData("https://example/flatcontainer/a/1.0.0/A.1.0.0.NUPKG")]
        [InlineData("https://example/flatcontainer/a/1.0.0/A.NUSPEC")]
        public void Compare_WithUpperCaseExtension_OrdersPackageFilesBeforeMiscFiles(string packageFileUri)
        {
            var packageFile = new PureSleetFile(packageFileUri);
            var misc = new PureSleetFile("https://example/catalog/data/a.json");
            var comparer = new SleetFileComparer();

            comparer.Compare(packageFile, misc).Should().BeNegative();
            comparer.Compare(misc, packageFile).Should().BePositive();
        }

        private sealed class PureSleetFile : ISleetFile
        {
            public PureSleetFile(string uri)
            {
                RootPath = new Uri(uri);
                EntityUri = new Uri(uri);
            }

            public Uri RootPath { get; }
            public Uri EntityUri { get; }
            public ISleetFileSystem FileSystem => throw new NotImplementedException();
            public bool HasChanges => false;
            public Task Write(Stream stream, ILogger log, CancellationToken token) => throw new NotImplementedException();
            public Task Push(ILogger log, CancellationToken token) => throw new NotImplementedException();
            public Task<bool> Exists(ILogger log, CancellationToken token) => throw new NotImplementedException();
            public Task<JObject> GetJson(ILogger log, CancellationToken token) => throw new NotImplementedException();
            public Task<JObject> GetJsonOrNull(ILogger log, CancellationToken token) => throw new NotImplementedException();
            public Task Write(JObject json, ILogger log, CancellationToken token) => throw new NotImplementedException();
            public void Link(string path, ILogger log, CancellationToken token) => throw new NotImplementedException();
            public void Delete(ILogger log, CancellationToken token) => throw new NotImplementedException();
            public Task<Stream> GetStream(ILogger log, CancellationToken token) => throw new NotImplementedException();
            public Task<bool> CopyTo(string path, bool overwrite, ILogger log, CancellationToken token) => throw new NotImplementedException();
            public Task FetchAsync(ILogger log, CancellationToken token) => throw new NotImplementedException();
            public Task<bool> ExistsWithFetch(ILogger log, CancellationToken token) => throw new NotImplementedException();
            public void Invalidate() => throw new NotImplementedException();
        }
    }
}
