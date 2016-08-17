// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace FileFormats.PE
{
    /// <summary>
    /// A very basic PE reader that can extract a few useful pieces of information
    /// </summary>
    public class PEFile
    {
        // PE file
        private readonly ulong _position;
        private readonly IAddressSpace _fileAddressSpace;
        private readonly Reader _peHeaderReader;
        private readonly Lazy<ushort> _dosHeaderMagic;
        private readonly Lazy<CoffFileHeader> _coffHeader;
        private readonly Lazy<uint> _peHeaderOffset;
        private readonly Lazy<uint> _peSignature;
        private readonly Lazy<PEOptionalHeaderMagic> _peHeaderOptionalHeaderMagic;
        private readonly Lazy<Reader> _peFileReader;
        private readonly Lazy<PEOptionalHeaderWindows> _peOptionalHeader;

        private const ushort ExpectedDosHeaderMagic = 0x5A4D;     // MZ
        private const int PESignatureOffsetLocation = 0x3C;
        private const uint ExpectedPESignature = 0x00004550;    // PE00

        public PEFile(IAddressSpace fileAddressSpace, ulong position = 0)
        {
            _position = position;
            _fileAddressSpace = fileAddressSpace;
            _peHeaderReader = new Reader(_fileAddressSpace);
            _dosHeaderMagic = new Lazy<ushort>(() => _peHeaderReader.Read<ushort>(_position));
            _peHeaderOffset = new Lazy<uint>(ReadPEHeaderOffset);
            _peSignature = new Lazy<uint>(() => _peHeaderReader.Read<uint>(_position + PEHeaderOffset));
            _coffHeader = new Lazy<CoffFileHeader>(ReadCoffFileHeader);
            _peHeaderOptionalHeaderMagic = new Lazy<PEOptionalHeaderMagic>(ReadPEOptionalHeaderMagic);
            _peFileReader = new Lazy<Reader>(CreatePEFileReader);
            _peOptionalHeader = new Lazy<PEOptionalHeaderWindows>(ReadPEOptionalHeaderWindows);
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

        private uint ReadPEHeaderOffset()
        {
            HasValidDosSignature.CheckThrowing();
            return _peHeaderReader.Read<uint>(_position + PESignatureOffsetLocation);
        }

        private CoffFileHeader ReadCoffFileHeader()
        {
            HasValidPESignature.CheckThrowing();
            return _peHeaderReader.Read<CoffFileHeader>(_position + PEHeaderOffset + 0x4);
        }

        private PEOptionalHeaderMagic ReadPEOptionalHeaderMagic()
        {
            ulong offset = _peHeaderReader.SizeOf<CoffFileHeader>() + PEHeaderOffset + 0x4;
            return _peHeaderReader.Read<PEOptionalHeaderMagic>(_position + offset);
        }

        private Reader CreatePEFileReader()
        {
            PEOptionalHeaderMagic.IsMagicValid.CheckThrowing();
            bool is64Bit = PEOptionalHeaderMagic.Magic == PEMagic.Magic32Plus;
            return new Reader(_fileAddressSpace, new LayoutManager().AddPETypes(is64Bit));
        }

        private PEOptionalHeaderWindows ReadPEOptionalHeaderWindows()
        {
            ulong offset = FileReader.SizeOf<CoffFileHeader>() + PEHeaderOffset + 0x4;
            return FileReader.Read<PEOptionalHeaderWindows>(_position + offset);
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
}
