// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using FileFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileFormats.ELF
{
    public enum ELFNoteType
    {
        PrpsInfo = 3,
        File = 0x46494c45 // "FILE" in ascii
    }

    public class ELFNoteHeader : TStruct
    {
        public uint NameSize;
        public uint ContentSize;
        public ELFNoteType Type;
    }
}
