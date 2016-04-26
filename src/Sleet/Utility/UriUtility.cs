using System;
using System.Diagnostics;
using System.IO;

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

            var combined = new Uri(AddTrailingSlash(root), relativePath);
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

        public static Uri AddTrailingSlash(string path)
        {
            return UriUtility.CreateUri(path.TrimEnd(new char[] { '/', '\\' }) + '/');
        }

        public static Uri AddTrailingSlash(Uri uri)
        {
            return AddTrailingSlash(uri.AbsoluteUri);
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
    }
}
