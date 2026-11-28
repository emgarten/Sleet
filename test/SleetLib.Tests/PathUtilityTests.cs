using System.IO;
using AwesomeAssertions;
using SleetLib;
using Xunit;

namespace SleetLib.Tests
{
    public class PathUtilityTests
    {
        [Fact]
        public void GetFullPathWithoutExtension_WithSimpleFile_RemovesExtension()
        {
            var path = "file.txt";

            var result = PathUtility.GetFullPathWithoutExtension(path);

            result.Should().Be("file");
        }

        [Fact]
        public void GetFullPathWithoutExtension_WithMultipleExtensions_RemovesLastExtension()
        {
            var path = "file.tar.gz";

            var result = PathUtility.GetFullPathWithoutExtension(path);

            result.Should().Be("file.tar");
        }

        [Fact]
        public void GetFullPathWithoutExtension_WithFullPath_RemovesExtensionKeepsPath()
        {
            var path = "/path/to/file.txt";

            var result = PathUtility.GetFullPathWithoutExtension(path);

            result.Should().Be("/path/to/file");
        }

        [Fact]
        public void GetFullPathWithoutExtension_WithWindowsPath_RemovesExtensionKeepsPath()
        {
            var path = "C:\\path\\to\\file.txt";

            var result = PathUtility.GetFullPathWithoutExtension(path);

            result.Should().Be("C:\\path\\to\\file");
        }

        [Fact]
        public void GetFullPathWithoutExtension_WithNoExtension_ReturnsOriginalPath()
        {
            var path = "/path/to/file";

            var result = PathUtility.GetFullPathWithoutExtension(path);

            result.Should().Be(path);
        }

        [Fact]
        public void GetFullPathWithoutExtension_WithDotInDirectory_OnlyRemovesFileExtension()
        {
            var path = "/path/to.dir/file.txt";

            var result = PathUtility.GetFullPathWithoutExtension(path);

            result.Should().Be("/path/to.dir/file");
        }

        [Fact]
        public void GetFullPathWithoutExtension_WithDotAtBeginning_ReturnsOriginalPath()
        {
            var path = ".hiddenfile";

            var result = PathUtility.GetFullPathWithoutExtension(path);

            result.Should().Be(path);
        }

        [Fact]
        public void GetFullPathWithoutExtension_WithDotInDirectoryNoFileExtension_ReturnsOriginalPath()
        {
            var path = "/path/to.dir/file";

            var result = PathUtility.GetFullPathWithoutExtension(path);

            result.Should().Be(path);
        }

        [Fact]
        public void GetFullPathWithoutExtension_WithEmptyString_ReturnsEmptyString()
        {
            var path = "";

            var result = PathUtility.GetFullPathWithoutExtension(path);

            result.Should().Be("");
        }

        [Fact]
        public void GetFullPathWithoutExtension_WithNull_ReturnsNull()
        {
            string path = null;

            var result = PathUtility.GetFullPathWithoutExtension(path);

            result.Should().BeNull();
        }

        [Fact]
        public void GetFullPathWithoutExtension_WithOnlyDot_ReturnsOriginalPath()
        {
            var path = ".";

            var result = PathUtility.GetFullPathWithoutExtension(path);

            result.Should().Be(path);
        }

        [Fact]
        public void GetFullPathWithoutExtension_WithDotAtEnd_ReturnsOriginalPath()
        {
            var path = "file.";

            var result = PathUtility.GetFullPathWithoutExtension(path);

            result.Should().Be("file.");
        }

        [Fact]
        public void GetFullPathWithoutExtension_WithRelativePathBackslash_DoesNotRemoveExtension()
        {
            var path = "..\\relative\\file.txt";

            var result = PathUtility.GetFullPathWithoutExtension(path);

            result.Should().Be("..\\relative\\file.txt");
        }

        [Fact]
        public void GetFullPathWithoutExtension_WithComplexExtension_RemovesExtension()
        {
            var path = "package.1.0.0.nupkg";

            var result = PathUtility.GetFullPathWithoutExtension(path);

            result.Should().Be("package.1.0.0");
        }

        [Theory]
        [InlineData("file.txt", "file")]
        [InlineData("path/file.json", "path/file")]
        [InlineData("file", "file")]
        [InlineData(".config", ".config")]
        [InlineData("file.", "file.")]
        [InlineData("", "")]
        [InlineData("dir.name/file.ext", "dir.name/file")]
        public void GetFullPathWithoutExtension_VariousInputs_ReturnsExpectedOutput(string input, string expected)
        {
            var result = PathUtility.GetFullPathWithoutExtension(input);

            result.Should().Be(expected);
        }

        [Fact]
        public void TryGetFullPathUnderRoot_WithChildPath_ReturnsFullPath()
        {
            var root = Path.Combine(Path.GetTempPath(), "output");

            var result = PathUtility.TryGetFullPathUnderRoot(root, Path.Combine("a", "a.1.0.0.nupkg"), out var fullPath);

            result.Should().BeTrue();
            fullPath.Should().Be(Path.Combine(Path.GetFullPath(root), "a", "a.1.0.0.nupkg"));
        }

        [Theory]
        [InlineData("../escape/escape.1.0.0.nupkg")]
        [InlineData("a/../../escape.1.0.0.nupkg")]
        [InlineData("../output2/a.1.0.0.nupkg")]
        [InlineData(".")]
        public void TryGetFullPathUnderRoot_WithPathOutsideRoot_ReturnsFalse(string relativePath)
        {
            var root = Path.Combine(Path.GetTempPath(), "output");

            var result = PathUtility.TryGetFullPathUnderRoot(root, relativePath, out var fullPath);

            result.Should().BeFalse();
            fullPath.Should().BeNull();
        }

        [Fact]
        public void TryGetFullPathUnderRoot_WithRootedPath_ReturnsFalse()
        {
            var root = Path.Combine(Path.GetTempPath(), "output");
            var rootedPath = Path.Combine(Path.GetTempPath(), "rooted", "a.1.0.0.nupkg");

            var result = PathUtility.TryGetFullPathUnderRoot(root, rootedPath, out _);

            result.Should().BeFalse();
        }
    }
}