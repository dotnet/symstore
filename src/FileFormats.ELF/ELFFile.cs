// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using FileFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileFormats.ELF
{
    public class ELFFile
    {
        private readonly IAddressSpace _dataSource;
        private readonly ulong _position;
        private readonly bool _isDataSourceVirtualAddressSpace;
        private readonly Lazy<ELFHeaderIdent> _ident;
        private readonly Lazy<Reader> _dataSourceReader;
        private readonly Lazy<ELFHeader> _header;
        private readonly Lazy<IEnumerable<ELFSegment>> _segments;
        private readonly Lazy<Reader> _virtualAddressReader;
        private readonly Lazy<byte[]> _buildId;

        public ELFFile(IAddressSpace dataSource, ulong position = 0, bool isDataSourceVirtualAddressSpace = false)
        {
            _dataSource = dataSource;
            _position = position;
            _isDataSourceVirtualAddressSpace = isDataSourceVirtualAddressSpace;
            _ident = new Lazy<ELFHeaderIdent>(() => new Reader(_dataSource).Read<ELFHeaderIdent>(_position));
            _dataSourceReader = new Lazy<Reader>(() => new Reader(_dataSource, new LayoutManager().AddELFTypes(IsBigEndian, Is64Bit)));
            _header = new Lazy<ELFHeader>(() => DataSourceReader.Read<ELFHeader>(_position));
            _segments = new Lazy<IEnumerable<ELFSegment>>(ReadSegments);
            _virtualAddressReader = new Lazy<Reader>(CreateVirtualAddressReader);
            _buildId = new Lazy<byte[]>(ReadBuildID);
        }

        public ELFHeaderIdent Ident { get { return _ident.Value; } }
        public ELFHeader Header { get { return _header.Value; } }
        private Reader DataSourceReader { get { return _dataSourceReader.Value; } }
        public IEnumerable<ELFSegment> Segments { get { return _segments.Value; } }
        public Reader VirtualAddressReader { get { return _virtualAddressReader.Value; } }
        public byte[] BuildID { get { return _buildId.Value; } }

        public bool IsBigEndian
        {
            get
            {
                Ident.IsIdentMagicValid.CheckThrowing();
                Ident.IsDataValid.CheckThrowing();
                return Ident.Data == ELFData.BigEndian;
            }
        }

        public bool Is64Bit
        {
            get
            {
                Ident.IsIdentMagicValid.CheckThrowing();
                Ident.IsClassValid.CheckThrowing();
                return (Ident.Class == ELFClass.Class64);
            }
        }

        private IEnumerable<ELFSegment> ReadSegments()
        {
            Header.IsProgramHeaderCountReasonable.CheckThrowing();
            IsHeaderProgramHeaderOffsetValid.CheckThrowing();
            IsHeaderProgramHeaderEntrySizeValid.CheckThrowing();
            List<ELFSegment> segments = new List<ELFSegment>();
            for (uint i = 0; i < Header.ProgramHeaderCount; i++)
            {
                segments.Add(new ELFSegment(DataSourceReader, _position,
                    _position + Header.ProgramHeaderOffset + i * Header.ProgramHeaderEntrySize, _isDataSourceVirtualAddressSpace));
            }
            return segments;
        }

        private Reader CreateVirtualAddressReader()
        {
            if (_isDataSourceVirtualAddressSpace)
            {
                return DataSourceReader;
            }
            else
            {
                return DataSourceReader.WithAddressSpace(new ELFVirtualAddressSpace(Segments));
            }
        }

        private byte[] ReadBuildID()
        {
            foreach (ELFSegment segment in Segments)
            {
                if (segment.Header.Type == ELFProgramHeaderType.Note)
                {
                    ELFNoteList noteList = new ELFNoteList(segment.Contents);
                    foreach (ELFNote note in noteList.Notes)
                    {
                        ELFNoteType type = note.Header.Type;
                        if (type == ELFNoteType.PrpsInfo && note.Name.Equals("GNU"))
                        {
                            return note.Contents.Read(0, (uint)note.Contents.Length);
                        }
                    }
                }
            }

            return null;
        }

        #region Validation Rules
        public ValidationRule IsHeaderProgramHeaderOffsetValid
        {
            get
            {
                return new ValidationRule("ELF Header ProgramHeaderOffset is invalid or elf file is incomplete", () =>
                                          {
                                              return Header.ProgramHeaderOffset < _dataSource.Length &&
                                                     Header.ProgramHeaderOffset + (ulong)(Header.ProgramHeaderEntrySize * Header.ProgramHeaderCount) <= _dataSource.Length;
                                          },
                                          IsHeaderProgramHeaderEntrySizeValid,
                                          Header.IsProgramHeaderCountReasonable);
            }
        }

        public ValidationRule IsHeaderProgramHeaderEntrySizeValid
        {
            get
            {
                return new ValidationRule("ELF Header ProgramHeaderEntrySize is invalid",
                                          () => Header.ProgramHeaderEntrySize == DataSourceReader.SizeOf<ELFProgramHeader>());
            }
        }
        #endregion
    }

    public class ELFVirtualAddressSpace : IAddressSpace
    {
        private readonly ELFSegment[] _segments;
        private readonly ulong _length;

        public ELFVirtualAddressSpace(IEnumerable<ELFSegment> segments)
        {
            _segments = segments.ToArray();
            _length = _segments.Max(s => s.Header.VirtualAddress + s.Header.VirtualSize);
        }

        public ulong Length { get { return _length; } }

        public uint Read(ulong position, byte[] buffer, uint bufferOffset, uint count)
        {
            for (int i = 0; i < _segments.Length; i++)
            {
                ELFProgramHeader header = _segments[i].Header;
                if (header.VirtualAddress <= position && position + count <= header.VirtualAddress + header.VirtualSize)
                {
                    ulong segmentOffset = position - header.VirtualAddress;
                    uint fileBytes = (uint)Math.Min(count, header.FileSize);
                    uint bytesRead = _segments[i].Contents.Read(segmentOffset, buffer, bufferOffset, fileBytes);

                    //zero the rest of the buffer if it is in the virtual address space but not the physical address space
                    if (bytesRead == fileBytes && fileBytes != count)
                    {
                        Array.Clear(buffer, (int)(bufferOffset + fileBytes), (int)(count - fileBytes));
                        bytesRead = count;
                    }
                    return bytesRead;
                }
            }

            throw new BadInputFormatException("Virtual address range is not mapped");
        }
    }

    public class ELFSegment
    {
        private readonly Reader _dataSourceReader;
        private readonly ulong _programHeaderOffset;
        private readonly Lazy<ELFProgramHeader> _header;
        private readonly Lazy<Reader> _contents;

        public ELFSegment(Reader dataSourceReader, ulong elfOffset, ulong programHeaderOffset, bool isDataSourceVirtualAddressSpace)
        {
            _dataSourceReader = dataSourceReader;
            _programHeaderOffset = programHeaderOffset;
            _header = new Lazy<ELFProgramHeader>(() => _dataSourceReader.Read<ELFProgramHeader>(_programHeaderOffset));
            //TODO: validate p_vaddr, p_offset, p_filesz
            if (isDataSourceVirtualAddressSpace)
            {
                _contents = new Lazy<Reader>(() => _dataSourceReader.WithRelativeAddressSpace(elfOffset + Header.VirtualAddress, Header.FileSize));
            }
            else
            {
                _contents = new Lazy<Reader>(() => _dataSourceReader.WithRelativeAddressSpace(elfOffset + Header.FileOffset, Header.FileSize));
            }
        }

        public ELFProgramHeader Header { get { return _header.Value; } }
        public Reader Contents { get { return _contents.Value; } }

        public override string ToString()
        {
            if (_header.IsValueCreated)
            {
                return "Segment@[" + Header.VirtualAddress.ToString() + "-" + (Header.VirtualAddress + Header.VirtualSize).ToString() + ")";
            }
            else
            {
                return "SegmentHeader@0x" + _programHeaderOffset.ToString("x");
            }
        }
    }

    public class ELFNoteList
    {
        private readonly Reader _elfSegmentReader;
        private readonly Lazy<IEnumerable<ELFNote>> _notes;

        public ELFNoteList(Reader elfSegmentReader)
        {
            _elfSegmentReader = elfSegmentReader;
            _notes = new Lazy<IEnumerable<ELFNote>>(ReadNotes);
        }

        public IEnumerable<ELFNote> Notes { get { return _notes.Value; } }

        private IEnumerable<ELFNote> ReadNotes()
        {
            List<ELFNote> notes = new List<ELFNote>();
            ulong position = 0;
            while (position < _elfSegmentReader.Length)
            {
                ELFNote note = new ELFNote(_elfSegmentReader, position);
                notes.Add(note);
                position += note.Size;
            }
            return notes;
        }
    }

    public class ELFNote
    {
        private readonly Reader _elfSegmentReader;
        private readonly ulong _noteHeaderOffset;
        private readonly Lazy<ELFNoteHeader> _header;
        private readonly Lazy<string> _name;
        private readonly Lazy<Reader> _contents;

        public ELFNote(Reader elfSegmentReader, ulong offset)
        {
            _elfSegmentReader = elfSegmentReader;
            _noteHeaderOffset = offset;
            _header = new Lazy<ELFNoteHeader>(() => _elfSegmentReader.Read<ELFNoteHeader>(_noteHeaderOffset));
            _name = new Lazy<string>(ReadName);
            _contents = new Lazy<Reader>(CreateContentsReader);
        }

        public ELFNoteHeader Header { get { return _header.Value; } }
        //TODO: validate these fields
        public uint Size { get { return HeaderSize + Align4(Header.NameSize) + Align4(Header.ContentSize); } }
        public string Name { get { return _name.Value; } }
        public Reader Contents { get { return _contents.Value; } }

        private uint HeaderSize
        {
            get { return _elfSegmentReader.LayoutManager.GetLayout<ELFNoteHeader>().Size; }
        }

        private string ReadName()
        {
            ulong nameOffset = _noteHeaderOffset + HeaderSize;
            return _elfSegmentReader.WithRelativeAddressSpace(nameOffset, Align4(Header.NameSize)).Read<string>(0);
        }

        private Reader CreateContentsReader()
        {
            ulong contentsOffset = _noteHeaderOffset + HeaderSize + Align4(Header.NameSize);
            return _elfSegmentReader.WithRelativeAddressSpace(contentsOffset, Align4(Header.ContentSize));
        }

        private uint Align4(uint x)
        {
            return (x + 3U) & ~3U;
        }
    }
}
