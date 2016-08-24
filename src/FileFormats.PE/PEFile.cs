// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Linq;

namespace FileFormats.PE
{
    public class PEPdbRecord
    {
        public static string Path { get; private set; } 
        public static Guid Signature { get; private set; }
        public static int Age { get; private set; }

        public PEPdbRecord(string path, Guid sig, int age)
        {
            Path = path;
            Signature = sig;
            Age = age;
        }
    }

    /// <summary>
    /// A very basic PE reader that can extract a few useful pieces of information
    /// </summary>
    public class PEFile
    {
        // PE file
        private readonly IAddressSpace _fileAddressSpace;
        private readonly Reader _peHeaderReader;
        private readonly Lazy<ushort> _dosHeaderMagic;
        private readonly Lazy<CoffFileHeader> _coffHeader;
        private readonly Lazy<uint> _peHeaderOffset;
        private readonly Lazy<uint> _peSignature;
        private readonly Lazy<PEOptionalHeaderMagic> _peHeaderOptionalHeaderMagic;
        private readonly Lazy<Reader> _peFileReader;
        private readonly Lazy<PEOptionalHeaderWindows> _peOptionalHeader;
        private readonly Lazy<List<PEImageDataDirectory>> _peImageDataDirectory;
        private readonly Lazy<PEPdbRecord> _pdb;
        private readonly Lazy<List<PESectionHeader>> _segments;
        private readonly Lazy<Reader> _virtualAddressReader;

        private const ushort ExpectedDosHeaderMagic = 0x5A4D;     // MZ
        private const int PESignatureOffsetLocation = 0x3C;
        private const uint ExpectedPESignature = 0x00004550;    // PE00
        private const int DebugDataDirectoryOffset = 6;
        private const int ComDataDirectoryOffset = 14;
        private const int ImageDataDirectoryCount = 15;

        public PEFile(IAddressSpace fileAddressSpace)
        {
            _fileAddressSpace = fileAddressSpace;
            _peHeaderReader = new Reader(_fileAddressSpace);
            _dosHeaderMagic = new Lazy<ushort>(() => _peHeaderReader.Read<ushort>(0));
            _peHeaderOffset = new Lazy<uint>(ReadPEHeaderOffset);
            _peSignature = new Lazy<uint>(() => _peHeaderReader.Read<uint>(PEHeaderOffset));
            _coffHeader = new Lazy<CoffFileHeader>(ReadCoffFileHeader);
            _peHeaderOptionalHeaderMagic = new Lazy<PEOptionalHeaderMagic>(ReadPEOptionalHeaderMagic);
            _peFileReader = new Lazy<Reader>(CreatePEFileReader);
            _peOptionalHeader = new Lazy<PEOptionalHeaderWindows>(ReadPEOptionalHeaderWindows);
            _peImageDataDirectory = new Lazy<List<PEImageDataDirectory>>(ReadImageDataDirectory);
            _pdb = new Lazy<PEPdbRecord>(ReadPdbInfo);
            _segments = new Lazy<List<PESectionHeader>>(ReadPESectionHeaders);
            _virtualAddressReader = new Lazy<Reader>(CreateVirtualAddressReader);
        }

        public ushort DosHeaderMagic { get { return _dosHeaderMagic.Value; } }
        public uint PEHeaderOffset { get { return _peHeaderOffset.Value; } }
        public uint PESignature { get { return _peSignature.Value; } }
        public CoffFileHeader CoffFileHeader { get { return _coffHeader.Value; } }
        public uint Timestamp { get { return CoffFileHeader.TimeDateStamp; } }
        public PEOptionalHeaderMagic PEOptionalHeaderMagic { get { return _peHeaderOptionalHeaderMagic.Value; } }
        public Reader FileReader { get { return _peFileReader.Value; } }
        public PEOptionalHeaderWindows OptionalHeader { get { return _peOptionalHeader.Value; } }
        public uint SizeOfImage { get { return OptionalHeader.SizeOfImage; } }
        public ReadOnlyCollection<PEImageDataDirectory> ImageDataDirectory { get { return _peImageDataDirectory.Value.AsReadOnly(); } }
        public PEPdbRecord Pdb { get { return _pdb.Value; } }
        public Reader VirtualAddressReader { get { return _virtualAddressReader.Value; } }
        public ReadOnlyCollection<PESectionHeader> Segments { get { return _segments.Value.AsReadOnly(); } }

        private uint ReadPEHeaderOffset()
        {
            HasValidDosSignature.CheckThrowing();
            return _peHeaderReader.Read<uint>(PESignatureOffsetLocation);
        }

        private uint PEOptionalHeaderOffset
        {
            get
            {
                return FileReader.SizeOf<CoffFileHeader>() + PEHeaderOffset + 0x4;
            }
        }

        private CoffFileHeader ReadCoffFileHeader()
        {
            HasValidPESignature.CheckThrowing();
            return _peHeaderReader.Read<CoffFileHeader>(PEHeaderOffset + 0x4);
        }

        private PEOptionalHeaderMagic ReadPEOptionalHeaderMagic()
        {
            ulong offset = PEOptionalHeaderOffset;
            return _peHeaderReader.Read<PEOptionalHeaderMagic>(offset);
        }

        private Reader CreatePEFileReader()
        {
            PEOptionalHeaderMagic.IsMagicValid.CheckThrowing();
            bool is64Bit = PEOptionalHeaderMagic.Magic == PEMagic.Magic32Plus;
            return new Reader(_fileAddressSpace, new LayoutManager().AddPETypes(is64Bit));
        }

        private PEOptionalHeaderWindows ReadPEOptionalHeaderWindows()
        {
            ulong offset = PEOptionalHeaderOffset;
            return FileReader.Read<PEOptionalHeaderWindows>(offset);
        }

        private List<PEImageDataDirectory> ReadImageDataDirectory()
        {
            ulong offset = PEOptionalHeaderOffset + FileReader.SizeOf<PEOptionalHeaderWindows>();

            PEImageDataDirectory[] result = _peHeaderReader.ReadArray<PEImageDataDirectory>(offset, ImageDataDirectoryCount);
            return new List<PEImageDataDirectory>(result);
        }



        private List<PESectionHeader> ReadPESectionHeaders()
        {
            ulong offset = PEOptionalHeaderOffset + CoffFileHeader.SizeOfOptionalHeader;
            List<PESectionHeader> result = new List<PESectionHeader>(FileReader.ReadArray<PESectionHeader>(offset, CoffFileHeader.NumberOfSections));
            return result;
        }

        private PEPdbRecord ReadPdbInfo()
        {
            PEImageDataDirectory imageDebugDirectory = ImageDataDirectory[DebugDataDirectoryOffset];

            uint size = FileReader.SizeOf<ImageDebugDirectory>();
            uint count = size / imageDebugDirectory.Size;

            ImageDebugDirectory[] debugDirectories = VirtualAddressReader.ReadArray<ImageDebugDirectory>(imageDebugDirectory.VirtualAddress, count);

            IEnumerable<ImageDebugDirectory> codeViewDirectories = debugDirectories.Where(d => d.Type == ImageDebugType.Codeview);

            foreach (ImageDebugDirectory directory in codeViewDirectories)
            {
                ulong position = directory.AddressOfRawData;
                CV_INFO_PDB70 pdb = VirtualAddressReader.Read<CV_INFO_PDB70>(ref position);
                if (pdb.CvSignature != CV_INFO_PDB70.PDB70CvSignature)
                    continue;

                string filename = VirtualAddressReader.Read<string>(position);
                return new PEPdbRecord(filename, pdb.Signature, pdb.Age);
            }

            return null;
        }

        private Reader CreateVirtualAddressReader()
        {
            return _peFileReader.Value.WithAddressSpace(new PEPhysicalAddressSpace(_fileAddressSpace, 0, Segments));
        }


        #region Validation Rules
        public ValidationRule HasValidDosSignature
        {
            get
            {
                return new ValidationRule("PE file does not have valid DOS header", () =>
                   DosHeaderMagic == ExpectedDosHeaderMagic);
            }
        }

        public ValidationRule HasValidPESignature
        {
            get
            {
                return new ValidationRule("PE file does not have a valid PE signature", () =>
                    PESignature == ExpectedPESignature);
            }
        }
        #endregion
    }


    public class PEPhysicalAddressSpace : PiecewiseAddressSpace
    {
        public PEPhysicalAddressSpace(IAddressSpace addressSpace, ulong baseAddress, IEnumerable<PESectionHeader> segments)
            : base(segments.Select(segment => ToRange(addressSpace, baseAddress, segment)).ToArray())
        {

        }

        private static PiecewiseAddressSpaceRange ToRange(IAddressSpace virtualAddressSpace, ulong baseAddress, PESectionHeader segment)
        {
            long value = (long)(segment.PointerToRawData - (segment.VirtualAddress + baseAddress));

            /*
            ulong actualSegmentLoadAddress = segment.LoadCommand.VMAddress - preferredVMBaseAddress + baseLoadAddress;
            return new PiecewiseAddressSpaceRange()
            {
                AddressSpace = new RelativeAddressSpace(virtualAddressSpace, actualSegmentLoadAddress, segment.LoadCommand.FileSize,
                                                            (long)(segment.LoadCommand.FileOffset - actualSegmentLoadAddress)),
                */
            return new PiecewiseAddressSpaceRange()
            {
                AddressSpace = new RelativeAddressSpace(virtualAddressSpace, baseAddress, segment.VirtualSize, (long)(segment.PointerToRawData - (segment.VirtualAddress + baseAddress))),
                Start = segment.PointerToRawData,
                Length = segment.SizeOfRawData
            };
        }
    }
}
