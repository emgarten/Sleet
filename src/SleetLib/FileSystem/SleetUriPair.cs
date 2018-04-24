using System;

namespace Sleet
{
    public class SleetUriPair
    {
        /// <summary>
        /// Actual root URI in container or on local disk
        /// </summary>
        public Uri Root { get; set; }

        /// <summary>
        /// Display URI
        /// </summary>
        public Uri BaseURI { get; set; }
    }
}
