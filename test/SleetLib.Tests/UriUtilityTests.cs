using System;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class UriUtilityTests
    {
        [Theory]
        [InlineData("http://testa.org/a/", "http://testa.org/a/blah.json", "blah.json")]
        [InlineData("http://testa.org/a", "http://testa.org/a/blah.json", "/blah.json")]
        [InlineData("http://testa.org/", "http://testa.org/blah.json", "blah.json")]
        [InlineData("http://testa.org/a/", "http://testa.org/a/b", "b")]
        [InlineData("http://testa.org/a/", "http://testa.org/a/b/", "b/")]
        public void UriUtility_GetRelativePath(string root, string full, string expected)
        {
            Assert.Equal(expected, UriUtility.GetRelativePath(new Uri(root), new Uri(full)));
        }

        [Theory]
        [InlineData("http://testa.org/a/", "blah.json", "http://testa.org/a/blah.json")]
        [InlineData("http://testa.org/a", "blah.json", "http://testa.org/a/blah.json")]
        [InlineData("http://testa.org/a/", "/blah.json", "http://testa.org/a/blah.json")]
        [InlineData("http://testa.org/a/", "/b/blah.json", "http://testa.org/a/b/blah.json")]
        public void UriUtility_GetPath(string root, string path, string expected)
        {
            Assert.Equal(expected, UriUtility.GetPath(new Uri(root), path).AbsoluteUri);
        }

        [Fact]
        public void UriUtility_GetPath_WithMultipleSegments_TrimsIntermediateSlashesAndPreservesFinalTrailingSlash()
        {
            var result = UriUtility.GetPath(new Uri("https://example/feed/"), "/packages/", "PackageA", "/1.0.0/");

            Assert.Equal("https://example/feed/packages/PackageA/1.0.0/", result.AbsoluteUri);
        }

        [Fact]
        public void UriUtility_GetPath_WithNoRelativePaths_Throws()
        {
            Assert.ThrowsAny<Exception>(() => UriUtility.GetPath(new Uri("https://example/feed/")));
        }

        [Theory]
        [InlineData("http://testa.org/a/", "http://testb.org/b/", "http://testa.org/a/blah.json", "http://testb.org/b/blah.json")]
        [InlineData("file:///c:/temp/", "http://testb.org/b/", "file:///c:/temp/blah.json", "http://testb.org/b/blah.json")]
        public void UriUtility_ChangeRoot(string origRoot, string destRoot, string fullPath, string expected)
        {
            Assert.Equal(expected, UriUtility.ChangeRoot(new Uri(origRoot), new Uri(destRoot), new Uri(fullPath)).AbsoluteUri);
        }

        [Theory]
        [InlineData("http://testa.org/a/", "http://testa.org/a/")]
        [InlineData("http://testa.org/a", "http://testa.org/a/")]
        [InlineData("http://testa.org/", "http://testa.org/")]
        [InlineData("http://testa.org", "http://testa.org/")]
        [InlineData("file:///tmp/", "file:///tmp/")]
        [InlineData("file:///tmp", "file:///tmp/")]
        [InlineData("file:///tmp/////", "file:///tmp/")]
        [InlineData("http://testa.org/////", "http://testa.org/")]
        public void UriUtility_EnsureTrailingSlash(string uri, string expected)
        {
            Assert.Equal(expected, UriUtility.EnsureTrailingSlash(new Uri(uri)).AbsoluteUri);
        }

        [Theory]
        [InlineData("https://example/feed", true, "https://example/feed/")]
        [InlineData("https://example/feed", false, "https://example/feed")]
        public void UriUtility_CreateUri_WithEnsureTrailingSlashFlag_ControlsTrailingSlash(string path, bool ensureTrailingSlash, string expected)
        {
            Assert.Equal(expected, UriUtility.CreateUri(path, ensureTrailingSlash).AbsoluteUri);
        }

        [Fact]
        public void UriUtility_CreateUri_WithMultiplePaths_CombinesSegments()
        {
            Assert.Equal("https://example/feed/a/b.json", UriUtility.CreateUri("https://example/feed/", "a", "b.json").AbsoluteUri);
        }

        [Fact]
        public void UriUtility_CreateUri_WithNoPaths_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => UriUtility.CreateUri(Array.Empty<string>()));
        }

        [Fact]
        public void UriUtility_CreateUri_WithUnixRootedPath_CreatesFileUriOnUnix()
        {
            if (System.IO.Path.DirectorySeparatorChar == '/')
            {
                Assert.Equal("file:///var/sleet/feed", UriUtility.CreateUri("/var/sleet/feed").AbsoluteUri);
            }
            else
            {
                Assert.Throws<UriFormatException>(() => UriUtility.CreateUri("/var/sleet/feed"));
            }
        }

        [Theory]
        [InlineData("https://example/feed/index.json", "index.json")]
        [InlineData("https://example/feed/", null)]
        public void UriUtility_GetFileName_ReturnsFileNameOnlyForFileUris(string uri, string expected)
        {
            Assert.Equal(expected, UriUtility.GetFileName(new Uri(uri)));
        }

        [Fact]
        public void UriUtility_GetPathWithoutFile_RemovesFileNameOnly()
        {
            Assert.Equal("https://example/feed/a/", UriUtility.GetPathWithoutFile(new Uri("https://example/feed/a/index.json")).AbsoluteUri);
            Assert.Equal("https://example/feed/a/", UriUtility.GetPathWithoutFile(new Uri("https://example/feed/a/")).AbsoluteUri);
        }

        [Fact]
        public void UriUtility_GetRelativePath_WithUnrelatedUri_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => UriUtility.GetRelativePath(new Uri("https://example/a/"), new Uri("https://example/b/file.json")));
        }
    }
}
