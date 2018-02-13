// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace FileFormats.PDB
{
    public class PDBFile
    {
        private readonly IAddressSpace _fileAddressSpace;
        private readonly Reader _pdbFileReader;
        private readonly Lazy<PDBFileHeader> _pdbFileHeader;
        private readonly Lazy<Reader[]> _streams;
        private readonly Lazy<PDBNameStream> _nameStream;

        public PDBFile(IAddressSpace fileAddressSpace)
        {
            _fileAddressSpace = fileAddressSpace;
            _pdbFileReader = new Reader(_fileAddressSpace);
            _pdbFileHeader = new Lazy<PDBFileHeader>(() => _pdbFileReader.Read<PDBFileHeader>(0));
            _streams = new Lazy<Reader[]>(ReadDirectory);
            _nameStream = new Lazy<PDBNameStream>(() => new PDBNameStream(Streams[1]));
        }

        public PDBFileHeader Header { get { return _pdbFileHeader.Value; } }
        public IList<Reader> Streams { get { return _streams.Value; } }
        public PDBNameStream NameStream { get { return _nameStream.Value; } }
        public uint Age { get { return NameStream.Header.Age; } }
        public Guid Signature { get { return new Guid(NameStream.Header.Guid); } }

        private Reader[] ReadDirectory()
        {
            Header.IsMagicValid.CheckThrowing();
            uint secondLevelPageCount = ToPageCount(Header.DirectorySize);
            ulong pageIndicesOffset = _pdbFileReader.SizeOf<PDBFileHeader>();
            PDBPagedAddressSpace secondLevelPageList = CreatePagedAddressSpace(_pdbFileReader.DataSource, pageIndicesOffset, secondLevelPageCount * 4);
            PDBPagedAddressSpace directoryContent = CreatePagedAddressSpace(secondLevelPageList, 0, Header.DirectorySize);

            Reader directoryReader = new Reader(directoryContent);
            ulong position = 0;
            uint countStreams = directoryReader.Read<uint>(ref position);
            uint[] streamSizes = directoryReader.ReadArray<uint>(ref position, countStreams);
            Reader[] streams = new Reader[countStreams];
            for (uint i = 0; i < streamSizes.Length; i++)
            {
                streams[i] = new Reader(CreatePagedAddressSpace(directoryContent, position, streamSizes[i]));
                position += ToPageCount(streamSizes[i]) * 4;
            }
            return streams;
        }

        private PDBPagedAddressSpace CreatePagedAddressSpace(IAddressSpace indicesData, ulong offset, uint length)
        {
            uint[] indices = new Reader(indicesData).ReadArray<uint>(offset, ToPageCount(length));
            return new PDBPagedAddressSpace(_pdbFileReader.DataSource, indices, Header.PageSize, length);
        }

        private uint ToPageCount(uint size)
        {
            return (Header.PageSize + size - 1) / Header.PageSize;
        }
    }

    /// <summary>
    /// Defines a virtual address paged address space that maps to an underlying physical
    /// paged address space with a different set of page Indices.
    /// </summary>
    /// <remarks>
    /// A paged address space is an address space where any address A can be converted
    /// to a page index and a page offset. A = index*page_size + offset.
    /// 
    /// This paged address space maps each virtual address to a physical address by
    /// remapping each virtual page to potentially different physical page. If V is
    /// the virtual page index then pageIndices[V] is the physical page index.
    /// 
    /// For example if pageSize is 0x100 and pageIndices is { 0x7, 0x9 } then
    /// virtual address 0x156 is:
    /// virtual page index 0x1, virtual offset 0x56
    /// physical page index 0x9, physical offset 0x56
    /// physical address is 0x956
    /// </remarks>
    internal class PDBPagedAddressSpace : IAddressSpace
    {
        private readonly IAddressSpace _physicalAddresses;
        private readonly uint[] _pageIndices;
        private readonly uint _pageSize;

        public PDBPagedAddressSpace(IAddressSpace physicalAddresses, uint[] pageIndices, uint pageSize, ulong length)
        {
            _physicalAddresses = physicalAddresses;
            _pageIndices = pageIndices;
            _pageSize = pageSize;
            Length = length;
        }

        public ulong Length { get; private set; }

        public uint Read(ulong position, byte[] buffer, uint bufferOffset, uint count)
        {
            if (position + count > Length)
            {
                throw new BadInputFormatException("Unexpected end of data: Expected " + count + " bytes.");
            }

            uint bytesRead = 0;
            while (bytesRead != count)
            {
                ulong virtualAddressToRead = position + bytesRead;
                uint virtualPageOffset;
                ulong physicalPosition = GetPhysicalAddress(position, out virtualPageOffset);
                uint pageBytesToRead = Math.Min(_pageSize - virtualPageOffset, count - bytesRead);
                uint pageBytesRead = _physicalAddresses.Read(physicalPosition, buffer, bufferOffset + bytesRead, pageBytesToRead);
                bytesRead += pageBytesRead;
                if (pageBytesToRead != pageBytesRead)
                {
                    break;
                }
            }
            return bytesRead;
        }

        private ulong GetPhysicalAddress(ulong virtualAddress, out uint virtualOffset)
        {
            uint virtualPageIndex = (uint)(virtualAddress / _pageSize);
            virtualOffset = (uint)(virtualAddress - (virtualPageIndex * _pageSize));
            uint physicalPageIndex = _pageIndices[(int)virtualPageIndex];
            return physicalPageIndex * _pageSize + virtualOffset;
        }
    }

    public class PDBNameStream
    {
        private readonly Reader _streamReader;
        private readonly Lazy<NameIndexStreamHeader> _header;


        public PDBNameStream(Reader streamReader)
        {
            _streamReader = streamReader;
            _header = new Lazy<NameIndexStreamHeader>(() => _streamReader.Read<NameIndexStreamHeader>(0));
        }

        public NameIndexStreamHeader Header { get { return _header.Value; } }
    }
}
