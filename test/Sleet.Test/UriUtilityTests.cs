using System;
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
    }
}