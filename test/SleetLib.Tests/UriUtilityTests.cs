using System;
using FluentAssertions;
using Sleet.Test.Common;
using Xunit;

namespace Sleet.Test
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

        [WindowsTheory]
        [InlineData(@".\", @"c:\otherPath\sleet.json", @"c:\otherPath\")]
        [InlineData(@".", @"c:\otherPath\sleet.json", @"c:\otherPath")]
        [InlineData(@"", @"c:\otherPath\sleet.json", @"c:\otherPath")]
        [InlineData(@"singleSubFolder", @"c:\otherPath\sleet.json", @"c:\otherPath\singleSubFolder")]
        [InlineData(@"nestedSubFolder\a", @"c:\otherPath\sleet.json", @"c:\otherPath\nestedSubFolder\a")]
        [InlineData(@"c:\absolutePath", @"c:\otherPath\sleet.json", @"c:\absolutePath")]
        public void UriUtility_GetAbsolutePath(string path, string settingsPath, string expected)
        {
            Assert.Equal(expected, UriUtility.GetAbsolutePath(path, settingsPath));
        }

        [Fact]
        public void UriUtility_ThrowsIfGetAbosolutePathWithNoSettingsFile()
        {
            Exception ex = null;

            try
            {
                UriUtility.GetAbsolutePath("", null);
            }
            catch (Exception e)
            {
                ex = e;
            }

            ex.Should().NotBeNull();
            ex.Message.Should().Be("Cannot use a relative 'path' without a settings.json file.");
        }
    }
}