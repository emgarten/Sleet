using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sleet
{
    /// <summary>
    /// Order files in a way that helps avoid clients discovering packages from index files
    /// before the package files have been uploaded.
    ///
    /// Order of upload:
    /// 1) Nupkg/nuspec files, these are usually the largest and slowest files
    /// 2) Misc files such as package details pages and catalog entry pages.
    /// 3) index.json files
    /// 4) Search page
    /// 
    /// </summary>
    public class SleetFileComparer : IComparer<ISleetFile>
    {
        public int Compare(ISleetFile x, ISleetFile y)
        {
            if (x == null)
            {
                throw new ArgumentNullException(nameof(x));
            }

            if (y == null)
            {
                throw new ArgumentNullException(nameof(y));
            }

            var priX = GetPriority(x);
            var priY = GetPriority(y);

            if (priX < priY)
            {
                return -1;
            }

            if (priX > priY)
            {
                return 1;
            }

            // Fallback to string compare
            return StringComparer.OrdinalIgnoreCase.Compare(x.EntityUri.AbsoluteUri, y.EntityUri.AbsoluteUri);
        }

        private static int GetPriority(ISleetFile file)
        {
            var fileName = file.EntityUri.AbsoluteUri.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Last().ToLowerInvariant();

            if (!string.IsNullOrEmpty(fileName))
            {
                if (fileName.IndexOf('.') > -1)
                {
                    var extension = Path.GetExtension(fileName).ToLowerInvariant();
                    switch (extension)
                    {
                        case "nupkg":
                            return 1;
                        case "nuspec":
                            return 2;
                    }
                }

                switch (fileName)
                {
                    case "index.json":
                        // Upload index.json files last after everything else is in place
                        return 7;
                    case "query":
                        // Update search after index files
                        return 8;
                }
            }

            return 5;
        }
    }
}
