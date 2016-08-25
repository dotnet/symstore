// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileFormats.PE
{
    public static class LayoutManagerExtensions
    {
        public static LayoutManager AddPETypes(this LayoutManager layouts, bool is64Bit)
        {
            return layouts
            .AddPrimitives(false)
            .AddEnumTypes()
            .AddSizeT(is64Bit ? 8 : 4)
            .AddNullTerminatedString()
            .AddTStructTypes(is64Bit ? new string[] { "PE32+" } : new string[] { "PE32" });
        }
    }

    public class CoffFileHeader : TStruct
    {
        public ushort Machine;
        public ushort NumberOfSections;
        public uint TimeDateStamp;
        public uint PointerToSymbolTable;
        public uint NumberOfSymbols;
        public ushort SizeOfOptionalHeader;
        public ushort Characteristics;
    }

    public enum PEMagic : ushort
    {
        Magic32 = 0x10b,
        Magic32Plus = 0x20b
    }

    public class PEOptionalHeaderMagic : TStruct
    {
        public PEMagic Magic;

        #region Validation Rules
        public ValidationRule IsMagicValid
        {
            get
            {
                return new ValidationRule("PE Optional Header has invalid magic field", () => Enum.IsDefined(typeof(PEMagic), Magic));
            }
        }
        #endregion
    }

    public class PEOptionalHeader : PEOptionalHeaderMagic
    {
        public byte MajorLinkerVersion;
        public byte MinorLinkerVersion;
        public uint SizeOfCode;
        public uint SizeOfInitializedData;
        public uint SizeOfUninitializedData;
        public uint RVAOfEntryPoint;
        public uint BaseOfCode;
        [If("PE32")]
        public uint BaseOfData;
    }

    public class PEOptionalHeaderWindows : PEOptionalHeader
    {
        public SizeT ImageBase;
        public uint SectionAlignment;
        public uint FileAlignment;
        public ushort MajorOperatingSystemVersion;
        public ushort MinorOperatingSystemVersion;
        public ushort MajorImageVersion;
        public ushort MinorImageVersion;
        public ushort MajorSubsystemVersion;
        public ushort MinorSubsystemVersion;
        public uint Win32VersionValue;
        public uint SizeOfImage;
        public uint SizeOfHeaders;
        public uint CheckSum;
        public ushort Subsystem;
        public ushort DllCharacteristics;
        public SizeT SizeOfStackReserve;
        public SizeT SizeOfStackCommit;
        public SizeT SizeOfHeapReserve;
        public SizeT SizeOfHeapCommit;
        public uint LoaderFlags;
        public uint NumberOfRvaAndSizes;
    }

    public class PEImageDataDirectory : TStruct
    {
        public uint VirtualAddress;
        public uint Size;
    }

    public class ImageDebugDirectory : TStruct
    {
        public uint Characteristics;
        public uint TimeDateStamp;
        public short MajorVersion;
        public short MinorVersion;
        public ImageDebugType Type;
        public uint SizeOfData;
        public uint AddressOfRawData;
        public uint PointerToRawData;
    };

    public enum ImageDebugType
    {
        Unknown = 0,
        Coff = 1,
        Codeview = 2,
        Fpo = 3,
        Misc = 4,
        Bbt = 10,
    };

    public class PESectionHeader : TStruct
    {
        [ArraySize(8)]
        public byte[] Name;
        public uint VirtualSize;
        public uint VirtualAddress;
        public uint SizeOfRawData;
        public uint PointerToRawData;
        public uint PointerToRelocations;
        public uint PointerToLinenumbers;
        public ushort NumberOfRelocations;
        public ushort NumberOfLinenumbers;
        public uint Characteristics;
    }

    internal class CV_INFO_PDB70 : TStruct
    {
        public const int PDB70CvSignature = 0x53445352; // RSDS in ascii
        public int CvSignature;
        [ArraySize(16)]
        public byte[] Signature;
        public int Age;
    }
}
