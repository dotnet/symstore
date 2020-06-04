// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.FileFormats;
using Microsoft.FileFormats.ELF;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.SymbolStore.KeyGenerators
{
    public class ELFFileKeyGenerator : KeyGenerator
    {
        private const string IdentityPrefix = "elf-buildid";
        private const string SymbolPrefix = "elf-buildid-sym";
        private const string CoreClrPrefix = "elf-buildid-coreclr";
        private const string CoreClrFileName = "libcoreclr.so";

        /// <summary>
        /// Symbol file extensions. The first one is the default symbol file extension used by .NET Core.
        /// </summary>
        private static readonly string[] s_symbolFileExtensions = { ".dbg", ".debug" };
        
        /// <summary>
        /// List of special clr files that are also indexed with libcoreclr.so's key.
        /// </summary>
        private static readonly string[] s_specialFiles = new string[] { "libmscordaccore.so", "libmscordbi.so", "mscordaccore.dll", "mscordbi.dll" };
        private static readonly string[] s_sosSpecialFiles = new string[] { "libsos.so", "SOS.NETCore.dll" };

        private static readonly HashSet<string> s_coreClrSpecialFiles = new HashSet<string>(s_specialFiles.Concat(s_sosSpecialFiles));
        private static readonly HashSet<string> s_dacdbiSpecialFiles = new HashSet<string>(s_specialFiles);

        private readonly ELFFile _elfFile;
        private readonly string _path;

        public ELFFileKeyGenerator(ITracer tracer, ELFFile elfFile, string path)
            : base(tracer)
        {
            _elfFile = elfFile;
            _path = path;
        }

        public ELFFileKeyGenerator(ITracer tracer, SymbolStoreFile file)
            : this(tracer, new ELFFile(new StreamAddressSpace(file.Stream)), file.FileName)
        {
        }

        public override bool IsValid()
        {
            return _elfFile.IsValid() &&
                (_elfFile.Header.Type == ELFHeaderType.Executable || _elfFile.Header.Type == ELFHeaderType.Shared || _elfFile.Header.Type == ELFHeaderType.Relocatable);
        }

        public override IEnumerable<SymbolStoreKey> GetKeys(KeyTypeFlags flags)
        {
            if (IsValid())
            {
                byte[] buildId = _elfFile.BuildID;
                if (buildId != null && buildId.Length == 20)
                {
                    bool symbolFile = false;
                    try
                    {
                        symbolFile = Array.Exists(_elfFile.Sections, section => (section.Name.StartsWith(".debug_info") || section.Name.StartsWith(".zdebug_info")));
                    }
                    catch (Exception ex) when (ex is InvalidVirtualAddressException)
                    {
                        // This could occur when trying to read sections for an ELF image grabbed from a core dump
                        // In that case, fallback to checking the file extension
                        symbolFile = Array.IndexOf(s_symbolFileExtensions, Path.GetExtension(_path)) != -1;
                    }

                    string symbolFileName = GetSymbolFileName();
                    foreach (SymbolStoreKey key in GetKeys(flags, _path, buildId, symbolFile, symbolFileName))
                    {
                        yield return key;
                    }
                    if ((flags & KeyTypeFlags.HostKeys) != 0)
                    {
                        if (_elfFile.Header.Type == ELFHeaderType.Executable)
                        {
                            // The host program as itself (usually dotnet)
                            yield return BuildKey(_path, IdentityPrefix, buildId);

                            // apphost downloaded as the host program name
                            yield return BuildKey(_path, IdentityPrefix, buildId, "apphost");
                        }
                    }
                }
                else
                {
                    Tracer.Error("Invalid ELF BuildID '{0}' for {1}", buildId == null ? "<null>" : ToHexString(buildId), _path);
                }
            }
        }

        /// <summary>
        /// Creates the ELF file symbol store keys.
        /// </summary>
        /// <param name="flags">type of keys to return</param>
        /// <param name="path">file name and path</param>
        /// <param name="buildId">ELF file uuid bytes</param>
        /// <param name="symbolFile">if true, use the symbol file tag</param>
        /// <param name="symbolFileName">name of symbol file (from .gnu_debuglink) or null</param>
        /// <returns>symbol store keys</returns>
        public static IEnumerable<SymbolStoreKey> GetKeys(KeyTypeFlags flags, string path, byte[] buildId, bool symbolFile, string symbolFileName)
        {
            Debug.Assert(path != null);
            Debug.Assert(buildId != null && buildId.Length == 20);

            if ((flags & KeyTypeFlags.IdentityKey) != 0)
            {
                if (symbolFile)
                {
                    yield return BuildKey(path, SymbolPrefix, buildId, "_.debug");
                }
                else
                {
                    bool clrSpecialFile = s_coreClrSpecialFiles.Contains(GetFileName(path));
                    yield return BuildKey(path, IdentityPrefix, buildId, clrSpecialFile);
                }
            }
            if (!symbolFile)
            {
                if ((flags & KeyTypeFlags.SymbolKey) != 0)
                {
                    if (string.IsNullOrEmpty(symbolFileName))
                    {
                        symbolFileName = path + s_symbolFileExtensions[0];
                    }
                    yield return BuildKey(symbolFileName, SymbolPrefix, buildId, "_.debug");
                }
                if ((flags & (KeyTypeFlags.ClrKeys | KeyTypeFlags.DacDbiKeys)) != 0)
                {
                    /// Creates all the special CLR keys if the path is the coreclr module for this platform
                    if (GetFileName(path) == CoreClrFileName)
                    {
                        foreach (string specialFileName in (flags & KeyTypeFlags.ClrKeys) != 0 ? s_coreClrSpecialFiles : s_dacdbiSpecialFiles)
                        {
                            yield return BuildKey(specialFileName, CoreClrPrefix, buildId);
                        }
                    }
                }
            }
        }

        private string GetSymbolFileName()
        {
            try
            {
                ELFSection section = _elfFile.FindSectionByName(".gnu_debuglink");
                if (section != null)
                {
                    return section.Contents.Read<string>(0);
                }
            }
            catch (Exception ex) when (ex is InvalidVirtualAddressException || ex is BadInputFormatException)
            {
                Tracer.Verbose("ELF .gnu_debuglink section in {0}: {1}", _path, ex.Message);
            }
            return null;
        }
    }
}
