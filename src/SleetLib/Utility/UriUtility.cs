using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Sleet
{
    public static class UriUtility
    {
        private static readonly char[] _dirChars = new char[] { '\\', '/' };

        /// <summary>
        /// Check if the URI has the expected root
        /// </summary>
        public static bool HasRoot(Uri expectedRoot, Uri fullPath, bool caseSensitive)
        {
            var compare = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            return fullPath.AbsoluteUri.StartsWith(expectedRoot.AbsoluteUri, compare);
        }

        /// <summary>
        /// Convert a URI from one root to another.
        /// </summary>
        public static Uri ChangeRoot(Uri origRoot, Uri destRoot, Uri origPath)
        {
            var relativePath = GetRelativePath(origRoot, origPath);

            return GetPath(destRoot, relativePath);
        }

        /// <summary>
        /// Combine a root and relative path
        /// </summary>
        public static Uri GetPath(Uri root, params string[] relativePaths)
        {
            if (relativePaths.Length < 1)
            {
                Debug.Fail("bad path");
                throw new ArgumentNullException(nameof(relativePaths));
            }

            var parts = new List<string>(relativePaths.Length);

            for (var i=0; i < relativePaths.Length; i++)
            {
                if ((i+1) == relativePaths.Length)
                {
                    // Leaving trailing slashes on the final piece
                    parts.Add(relativePaths[i].TrimStart(_dirChars));
                }
                else
                {
                    parts.Add(relativePaths[i].Trim(_dirChars));
                }
            }
            var combined = new Uri(EnsureTrailingSlash(root), string.Join("/", parts));
            return combined;
        }

        public static string GetRelativePath(Uri basePath, Uri path)
        {
            if (path.AbsoluteUri.StartsWith(basePath.AbsoluteUri))
            {
                return path.AbsoluteUri.Substring(basePath.AbsoluteUri.Length);
            }

            throw new ArgumentException($"Uri {path} is not rooted in the basePath: {basePath}");
        }

        /// <summary>
        /// Create a URI in a safe manner that works for UNIX file paths.
        /// </summary>
        public static Uri CreateUri(params string[] paths)
        {
            if (paths.Length == 0)
            {
                throw new ArgumentException(nameof(paths));
            }

            var root = CreateUri(paths[0]);

            if (paths.Length < 1)
            {
                return root;
            }

            return GetPath(root, paths.Skip(1).ToArray());
        }

        /// <summary>
        /// Create a URI in a safe manner that works for UNIX file paths.
        /// </summary>
        public static Uri CreateUri(string path)
        {
            if (Path.DirectorySeparatorChar == '/' && !string.IsNullOrEmpty(path) && path[0] == '/')
            {
                return new Uri("file://" + path);
            }

            return new Uri(path);
        }

        /// <summary>
        /// Create a URI in a safe manner that works for UNIX file paths.
        /// </summary>
        public static Uri CreateUri(string path, bool ensureTrailingSlash)
        {
            var uri = CreateUri(path);

            if (ensureTrailingSlash)
            {
                uri = EnsureTrailingSlash(uri);
            };

            return uri;
        }

        public static Uri EnsureTrailingSlash(Uri uri)
        {
            var s = uri.AbsoluteUri;

            if (s.EndsWith("//", StringComparison.OrdinalIgnoreCase))
            {
                // Trim if needed
                s = s.TrimEnd('/');
            }

            if (s.EndsWith("/", StringComparison.Ordinal))
            {
                // noop
                return uri;
            }

            return new Uri(s + "/");
        }

        public static Uri RemoveTrailingSlash(Uri uri)
        {
            var s = uri.AbsoluteUri;

            if (!s.EndsWith("/", StringComparison.Ordinal))
            {
                // noop
                return uri;
            }

            return new Uri(s.TrimEnd('/'));
        }

        public static bool IsHttp(Uri uri)
        {
            return (uri.AbsoluteUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || uri.AbsoluteUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the file name from a Uri or null if this is a directory.
        /// </summary>
        public static string GetFileName(Uri uri)
        {
            var absolute = uri.AbsoluteUri;

            if (absolute.EndsWith("/"))
            {
                return null;
            }
            else
            {
                return uri.AbsoluteUri.Split('/').Last();
            }
        }
    }
}