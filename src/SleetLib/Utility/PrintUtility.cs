using System;

namespace Sleet
{
    public static class PrintUtility
    {
        public static string GetBytesString(long bytes)
        {
            if (bytes > (1024 * 1024))
            {
                return $"{Math.Round((bytes / 1024f / 1024f), 2)} MB";
            }

            return $"{Math.Round((bytes / 1024f), 2)} KB";
        }

        public static string GetTimeString(TimeSpan span)
        {
            var result = string.Empty;

            if (span.TotalMilliseconds <= 90000)
            {
                result = $"{Math.Ceiling(span.TotalMilliseconds)} ms";
            }
            else
            {
                // Use the time as is
                result = span.ToString();
            }

            return result.Trim();
        }
    }
}
