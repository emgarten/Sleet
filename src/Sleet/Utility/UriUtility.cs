using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Sleet
{
    public static class UriUtility
    {
        /// <summary>
        /// Check if the URI has the expected root
        /// </summary>
        public static bool HasRoot(Uri expectedRoot, Uri fullPath)
        {
            return fullPath.AbsoluteUri.StartsWith(expectedRoot.AbsoluteUri);
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
        public static Uri GetPath(Uri root, string relativePath)
        {
            if (relativePath == null)
            {
                Debug.Fail("bad path");
                throw new ArgumentNullException(nameof(relativePath));
            }

            relativePath = relativePath.TrimStart(new char[] { '\\', '/' });

            var combined = new Uri(EnsureTrailingSlash(root), relativePath);
            return combined;
        }

        public static string GetRelativePath(Uri basePath, Uri path)
        {
            if (path.AbsoluteUri.StartsWith(basePath.AbsoluteUri))
            {
                return path.AbsoluteUri.Substring(basePath.AbsoluteUri.Length);
            }

            throw new ArgumentException("Uri is not rooted in the basePath");
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
            return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
        }

        public static Uri RemoveTrailingSlash(Uri uri)
        {
            return new Uri(uri.AbsoluteUri.TrimEnd('/'));
        }

        public static bool IsHttp(Uri uri)
        {
            return (uri.AbsoluteUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || uri.AbsoluteUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }
    }
}