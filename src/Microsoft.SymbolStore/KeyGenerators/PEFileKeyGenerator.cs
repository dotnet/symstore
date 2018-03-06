// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.FileFormats;
using Microsoft.FileFormats.PE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.SymbolStore.KeyGenerators
{
    public class PEFileKeyGenerator : KeyGenerator
    {
        private const string CoreClrFileName = "coreclr.dll";

        private static HashSet<string> s_longNameBinaryPrefixes = new HashSet<string>(new string[] { "mscordaccore_", "sos_" });
        private static HashSet<string> s_coreClrSpecialFiles = new HashSet<string>(new string[] { "mscordaccore.dll", "mscordbi.dll", "sos.dll", "SOS.NETCore.dll" });

        private readonly PEFile _peFile;
        private readonly string _path;

        public PEFileKeyGenerator(ITracer tracer, PEFile peFile, string path)
            : base(tracer)
        {
            _peFile = peFile;
            _path = path;
        }

        public PEFileKeyGenerator(ITracer tracer, SymbolStoreFile file)
            : this(tracer, new PEFile(new StreamAddressSpace(file.Stream)), file.FileName)
        {
        }

        public override bool IsValid()
        {
            return _peFile.IsValid();
        }

        public override IEnumerable<SymbolStoreKey> GetKeys(KeyTypeFlags flags)
        {
            if (IsValid())
            {
                if ((flags & KeyTypeFlags.IdentityKey) != 0)
                {
                    yield return GetKey(_path, _peFile.Timestamp, _peFile.SizeOfImage);
                }
                if ((flags & KeyTypeFlags.SymbolKey) != 0)
                {
                    foreach (PEPdbRecord pdb in _peFile.Pdbs)
                    {
                        if (((flags & KeyTypeFlags.ForceWindowsPdbs) == 0) && pdb.IsPortablePDB)
                        {
                            yield return PortablePDBFileKeyGenerator.GetKey(pdb.Path, pdb.Signature);
                        }
                        else
                        {
                            yield return PDBFileKeyGenerator.GetKey(pdb.Path, pdb.Signature, pdb.Age);
                        }
                    }
                }
                if ((flags & KeyTypeFlags.ClrKeys) != 0)
                {
                    if (Path.GetFileName(_path) == CoreClrFileName)
                    {
                        string coreclrId = string.Format("{0:x}{1:x}", _peFile.Timestamp, _peFile.SizeOfImage);
                        foreach (string specialFileName in GetSpecialFiles())
                        {
                            yield return BuildKey(specialFileName, coreclrId);
                        }
                    }
                }
            }
        }

        private IEnumerable<string> GetSpecialFiles()
        {
            var specialFiles = new List<string>(s_coreClrSpecialFiles);

            VsFixedFileInfo fileVersion = _peFile.VersionInfo;
            if (fileVersion != null)
            {
                ushort major = fileVersion.ProductVersionMajor;
                ushort minor = fileVersion.ProductVersionMinor;
                ushort build = fileVersion.ProductVersionBuild;
                ushort revision = fileVersion.ProductVersionRevision;
                string targetArch = null;

                ImageFileMachine machine = (ImageFileMachine)_peFile.FileHeader.Machine;
                switch (machine)
                {
                    case ImageFileMachine.Amd64:
                        targetArch = "amd64";
                        break;

                    case ImageFileMachine.I386:
                        targetArch = "x86";
                        break;

                    case ImageFileMachine.ArmNT:
                        targetArch = "arm";
                        break;

                    case ImageFileMachine.Arm64:
                        targetArch = "arm64";
                        break;
                }

                if (targetArch != null)
                {
                    string buildFlavor = "";

                    if ((fileVersion.FileFlags & FileInfoFlags.Debug) != 0)
                    {
                        if ((fileVersion.FileFlags & FileInfoFlags.SpecialBuild) != 0)
                        {
                            buildFlavor = ".dbg";
                        }
                        else
                        {
                            buildFlavor = ".chk";
                        }
                    }

                    foreach (string name in s_longNameBinaryPrefixes)
                    {
                        // The name prefixes include the trailing "_".
                        string longName = string.Format("{0}{1}_{2}_{3}.{4}.{5}.{6:00}{7}.dll",
                            name, targetArch, targetArch, major, minor, build, revision, buildFlavor);
                        specialFiles.Add(longName);
                    }
                }
            }
            else
            {
                Tracer.Warning("{0} has no version resource", _path);
            }

            return specialFiles;
        }

        /// <summary>
        /// Creates a PE file symbol store key identity key.
        /// </summary>
        /// <param name="path">file name and path</param>
        /// <param name="timestamp">time stamp of pe image</param>
        /// <param name="sizeOfImage">size of pe image</param>
        /// <returns>symbol store keys (or empty enumeration)</returns>
        public static SymbolStoreKey GetKey(string path, uint timestamp, uint sizeOfImage)
        {
            Debug.Assert(path != null);

            // The clr special file flag can not be based on the GetSpecialFiles() list because 
            // that is only valid when "path" is the coreclr.dll.
            string fileName = Path.GetFileName(path);
            bool clrSpecialFile = s_coreClrSpecialFiles.Contains(fileName) || 
                (s_longNameBinaryPrefixes.Any((prefix) => fileName.StartsWith(prefix)) && Path.GetExtension(fileName) == ".dll");

            string id = string.Format("{0:x}{1:x}", timestamp, sizeOfImage);
            return BuildKey(path, id, clrSpecialFile);
        }
    }
}