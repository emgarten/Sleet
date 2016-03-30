using System;
using System.Diagnostics;

namespace Sleet
{
    public static class UriUtility
    {
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

            var combined = new Uri(root, relativePath);
            return combined;
        }

        public static string GetRelativePath(Uri basePath, Uri path)
        {
            if (path.AbsoluteUri.StartsWith(path.AbsoluteUri))
            {
                return path.AbsoluteUri.Substring(basePath.AbsoluteUri.Length);
            }

            throw new ArgumentException("Uri is not rooted in the basePath");
        }

        public static Uri AddTrailingSlash(string path)
        {
            return new Uri(path.TrimEnd(new char[] { '/', '\\' }) + '/');
        }

        public static Uri AddTrailingSlash(Uri uri)
        {
            return AddTrailingSlash(uri.AbsoluteUri);
        }
    }
}
