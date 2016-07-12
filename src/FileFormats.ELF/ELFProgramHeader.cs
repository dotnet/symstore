// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using FileFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileFormats.ELF
{
    public enum ELFProgramHeaderType : uint
    {
        Null = 0,
        Load = 1,
        Dynamic = 2,
        Interp = 3,
        Note = 4,
        Shlib = 5,
        Phdr = 6
    }

    public class ELFProgramHeader : TStruct
    {
        public ELFProgramHeaderType Type;
        public uint Flags;
        public FileOffset FileOffset;
        public VirtualAddress VirtualAddress;
        public SizeT PhysicalAddress;
        public SizeT FileSize;
        public SizeT VirtualSize;
        public SizeT Alignment;
    }
}
