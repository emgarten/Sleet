using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Sleet;

namespace SleetLib
{
    public static class PathUtility
    {
        public static string GetFullPathWithoutExtension(string path)
        {
            if (string.IsNullOrEmpty(path) || path.IndexOf('.') < 1 || path.LastIndexOf('.') < path.LastIndexOf('/'))
            {
                return path;
            }

            var ext = Path.GetExtension(path)?.Length ?? 0;
            var len = path.Length - ext;

            return path.Substring(0, len);
        }

        /// <summary>
        /// Combine a root folder and a relative path into a full path.
        /// Returns false if the full path is not under the root folder.
        /// </summary>
        public static bool TryGetFullPathUnderRoot(string root, string relativePath, [NotNullWhen(true)] out string? fullPath)
        {
            var fullRoot = Path.GetFullPath(root);

            if (!Path.EndsInDirectorySeparator(fullRoot))
            {
                fullRoot += Path.DirectorySeparatorChar;
            }

            // Path.Combine returns relativePath as is when it is rooted, the check below rejects it.
            var path = Path.GetFullPath(Path.Combine(fullRoot, relativePath));

            if (path.Length > fullRoot.Length && path.StartsWith(fullRoot, StringComparison.Ordinal))
            {
                fullPath = path;
                return true;
            }

            fullPath = null;
            return false;
        }
    }
}
