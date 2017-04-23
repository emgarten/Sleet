using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection.PortableExecutable;
using System.IO.Compression;
using System.Linq;
using System.IO;

namespace Sleet
{
    public static class SymbolsUtility
    {
        // SymbolLib.pdb/4B26B9A60D384F90855C3A6196C6C8781/SymbolLib.pdb
        public static string GetSymbolsServerPath(string fileName, string hash)
        {
            if (string.IsNullOrEmpty(fileName) || fileName.IndexOf('.') == -1 || fileName.IndexOf('/') > -1)
            {
                throw new ArgumentException($"Invalid file name: {fileName}");
            }

            return $"{fileName}/{hash.ToLowerInvariant()}/{fileName}";
        }

        /// <summary>
        /// Returns the hash for the DLL file.
        /// </summary>
        public static string GetSymbolHashFromAssembly(PEReader peReader)
        {
            var size = peReader.PEHeaders.PEHeader.SizeOfImage;
            var time = peReader.PEHeaders.CoffHeader.TimeDateStamp;
            return string.Format("{0:X}{1:X}", time, size).ToUpperInvariant();
        }

        /// <summary>
        /// Returns the hash of the PDB from the DLL file.
        /// </summary>
        public static string GetPDBHashFromAssembly(PEReader peReader)
        {
            foreach (var entry in peReader.ReadDebugDirectory())
            {
                if (entry.Type == DebugDirectoryEntryType.CodeView)
                {
                    var codeViewData = peReader.ReadCodeViewDebugDirectoryData(entry);
                    var age = codeViewData.Age;
                    var guid = codeViewData.Guid;

                    if (guid != Guid.Empty)
                    {
                        var guidString = guid.ToString().Replace("-", string.Empty);
                        return string.Format("{0}{1:X}", guidString, age).ToUpperInvariant();
                    }
                }
            }

            return null;
        }
    }
}
