// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.FileFormats;
using Microsoft.FileFormats.MachO;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.SymbolStore.KeyGenerators
{
    public class MachOFileKeyGenerator : KeyGenerator
    {
        /// <summary>
        /// The default symbol file extension used by .NET Core.
        /// </summary>
        private const string SymbolFileExtension = ".dwarf";

        private const string IdentityPrefix = "mach-uuid";
        private const string SymbolPrefix = "mach-uuid-sym";
        private const string CoreClrPrefix = "mach-uuid-coreclr";
        private const string CoreClrFileName = "libcoreclr.dylib";

        private static HashSet<string> s_coreClrSpecialFiles = new HashSet<string>(new string[] { "libmscordaccore.dylib", "libmscordbi.dylib", "libsos.dylib", "SOS.NETCore.dll" });

        private readonly MachOFile _machoFile;
        private readonly string _path;

        public MachOFileKeyGenerator(ITracer tracer, MachOFile machoFile, string path)
            : base(tracer)
        {
            _machoFile = machoFile;
            _path = path;
        }

        public MachOFileKeyGenerator(ITracer tracer, SymbolStoreFile file)
            : this(tracer, new MachOFile(new StreamAddressSpace(file.Stream)), file.FileName)
        {
        }

        public override bool IsValid()
        {
            return _machoFile.IsValid() && 
                (_machoFile.Header.FileType == MachHeaderFileType.Execute || 
                 _machoFile.Header.FileType == MachHeaderFileType.Dylib ||
                 _machoFile.Header.FileType == MachHeaderFileType.Dsym);
        }

        public override IEnumerable<SymbolStoreKey> GetKeys(KeyTypeFlags flags)
        {
            if (IsValid())
            {
                byte[] uuid = _machoFile.Uuid;
                if (uuid != null && uuid.Length == 16)
                {
                    bool symbolFile = _machoFile.Header.FileType == MachHeaderFileType.Dsym;
                    // TODO - mikem 1/23/18 - is there a way to get the name of the "linked" dwarf symbol file
                    foreach (SymbolStoreKey key in GetKeys(flags, _path, uuid, symbolFile, symbolFileName: null))
                    {
                        yield return key;
                    }
                }
                else
                {
                    Tracer.Error("Invalid MachO uuid {0}", _path);
                }
            }
        }

        /// <summary>
        /// Creates the MachO file symbol store keys.
        /// </summary>
        /// <param name="flags">type of keys to return</param>
        /// <param name="path">file name and path</param>
        /// <param name="uuid">macho file uuid bytes</param>
        /// <param name="symbolFile">if true, use the symbol file tag</param>
        /// <param name="symbolFileName">name of symbol file or null</param>
        /// <returns>symbol store keys</returns>
        public static IEnumerable<SymbolStoreKey> GetKeys(KeyTypeFlags flags, string path, byte[] uuid, bool symbolFile, string symbolFileName)
        {
            Debug.Assert(path != null);
            Debug.Assert(uuid != null && uuid.Length == 16);

            if ((flags & KeyTypeFlags.IdentityKey) != 0)
            {
                if (symbolFile)
                {
                    yield return BuildKey(path, SymbolPrefix, uuid, "_.dwarf");
                }
                else
                {
                    bool clrSpecialFile = s_coreClrSpecialFiles.Contains(Path.GetFileName(path));
                    yield return BuildKey(path, IdentityPrefix, uuid, clrSpecialFile);
                }
            }
            if (!symbolFile)
            {
                if ((flags & KeyTypeFlags.SymbolKey) != 0)
                {
                    if (string.IsNullOrEmpty(symbolFileName))
                    {
                        symbolFileName = path + SymbolFileExtension;
                    }
                    yield return BuildKey(symbolFileName, SymbolPrefix, uuid, "_.dwarf");
                }
                if ((flags & KeyTypeFlags.ClrKeys) != 0)
                {
                    /// Creates all the special CLR keys if the path is the coreclr module for this platform
                    if (Path.GetFileName(path) == CoreClrFileName)
                    {
                        foreach (string specialFileName in s_coreClrSpecialFiles)
                        {
                            yield return BuildKey(specialFileName, CoreClrPrefix, uuid);
                        }
                    }
                }
            }
        }
    }
}
