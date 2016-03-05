using System;

namespace Sleet
{
    public static class Extensions
    {
        /// <summary>
        /// Convert to ISO format for json.
        /// </summary>
        public static string GetDateString(this DateTimeOffset date)
        {
            return date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        }
    }
}
