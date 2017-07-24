using System.IO;

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
    }
}
