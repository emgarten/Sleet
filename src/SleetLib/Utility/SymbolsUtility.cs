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

            return $"{fileName}/{hash}/{fileName}";
        }

        /// <summary>
        /// Returns the hash for the DLL file.
        /// </summary>
        public static string GetSymbolHashFromAssembly(PEReader peReader)
        {
            var size = peReader.PEHeaders.PEHeader.SizeOfImage;
            var time = peReader.PEHeaders.CoffHeader.TimeDateStamp;

            var timeHash = string.Format("{0:X}", time).ToUpperInvariant();
            var sizeHash = string.Format("{0:X}", size).ToLowerInvariant();

            return $"{timeHash}{sizeHash}";
        }

        /// <summary>
        /// Returns the hash of the PDB from the DLL file.
        /// </summary>
        public static string GetPDBHashFromAssembly(PEReader peReader)
        {
            string hash = null;

            foreach (var entry in peReader.ReadDebugDirectory())
            {
                if (entry.Type == DebugDirectoryEntryType.CodeView)
                {
                    var codeViewData = peReader.ReadCodeViewDebugDirectoryData(entry);
                    var age = codeViewData.Age;
                    var guid = codeViewData.Guid;
                    var portablePdb = entry.MinorVersion == 20557 && entry.MajorVersion >= 256;

                    if (guid != Guid.Empty)
                    {
                        if (portablePdb)
                        {
                            if (age == 1)
                            {
                                // Age must be 1 for portable pdbs
                                hash = GetPortablePdbHash(guid);
                            }
                        }
                        else
                        {
                            // Legacy pdb
                            hash = GetWindowsPdbHash(guid, age);
                        }
                    }
                }
            }

            return hash;
        }

        public static string GetPortablePdbHash(Guid guid)
        {
            return guid.ToString("N").ToUpperInvariant() + "ffffffff";
        }

        public static string GetWindowsPdbHash(Guid guid, int age)
        {
            return guid.ToString("N").ToUpperInvariant() + age;
        }
    }
}
