// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.FileFormats.ELF
{
    public class ELFFile
    {
        private readonly ulong _position;
        private readonly bool _isDataSourceVirtualAddressSpace;
        private readonly Reader _reader;
        private readonly Lazy<ELFHeaderIdent> _ident;
        private readonly Lazy<Reader> _dataSourceReader;
        private readonly Lazy<ELFHeader> _header;
        private readonly Lazy<IEnumerable<ELFProgramSegment>> _segments;
        private readonly Lazy<ELFSection[]> _sections;
        private readonly Lazy<Reader> _virtualAddressReader;
        private readonly Lazy<byte[]> _buildId;
        private readonly Lazy<byte[]> _sectionNameTable;

        public ELFFile(IAddressSpace dataSource, ulong position = 0, bool isDataSourceVirtualAddressSpace = false)
        {
            _position = position;
            _reader = new Reader(dataSource);
            _isDataSourceVirtualAddressSpace = isDataSourceVirtualAddressSpace;
            _ident = new Lazy<ELFHeaderIdent>(() => _reader.Read<ELFHeaderIdent>(_position));
            _dataSourceReader = new Lazy<Reader>(() => new Reader(dataSource, new LayoutManager().AddELFTypes(IsBigEndian, Is64Bit)));
            _header = new Lazy<ELFHeader>(() => DataSourceReader.Read<ELFHeader>(_position));
            _segments = new Lazy<IEnumerable<ELFProgramSegment>>(ReadSegments);
            _sections = new Lazy<ELFSection[]>(ReadSections);
            _virtualAddressReader = new Lazy<Reader>(CreateVirtualAddressReader);
            _buildId = new Lazy<byte[]>(ReadBuildId);
            _sectionNameTable = new Lazy<byte[]>(ReadSectionNameTable);
        }

        public ELFHeaderIdent Ident { get { return _ident.Value; } }
        public ELFHeader Header { get { return _header.Value; } }
        private Reader DataSourceReader { get { return _dataSourceReader.Value; } }
        public IEnumerable<ELFProgramSegment> Segments { get { return _segments.Value; } }
        public ELFSection[] Sections { get { return _sections.Value; } }
        public Reader VirtualAddressReader { get { return _virtualAddressReader.Value; } }
        public byte[] BuildID { get { return _buildId.Value; } }
        public byte[] SectionNameTable { get { return _sectionNameTable.Value; } }

        public bool IsValid()
        {
            if (_reader.Length > (_position + _reader.SizeOf<ELFHeaderIdent>()))
            {
                try {
                    return Ident.IsIdentMagicValid.Check();
                }
                catch (InvalidVirtualAddressException)
                {
                }
            }
            return false;
        }

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

        public ulong PreferredVMBaseAddress
        {
            get
            {
                ulong minAddr = ulong.MaxValue;

                foreach(var segment in Segments)
                {
                    if(segment.Header.Type == ELFProgramHeaderType.Load)
                    {
                        minAddr = Math.Min(minAddr, segment.Header.VirtualAddress);
                    }
                }

                return minAddr;
            }
        }

        public ELFSection FindSectionByName(string name)
        {
            foreach (ELFSection section in Sections)
            {
                if (string.Equals(section.Name, name))
                {
                    return section;
                }
            }
            return null;
        }

        private IEnumerable<ELFProgramSegment> ReadSegments()
        {
            Header.IsProgramHeaderCountReasonable.CheckThrowing();
            IsHeaderProgramHeaderOffsetValid.CheckThrowing();
            IsHeaderProgramHeaderEntrySizeValid.CheckThrowing();

            List<ELFProgramSegment> segments = new List<ELFProgramSegment>();
            for (uint i = 0; i < Header.ProgramHeaderCount; i++)
            {
                segments.Add(new ELFProgramSegment(DataSourceReader, _position,
                    _position + Header.ProgramHeaderOffset + i * Header.ProgramHeaderEntrySize, _isDataSourceVirtualAddressSpace));
            }
            return segments;
        }

        private ELFSection[] ReadSections()
        {
            Header.IsSectionHeaderCountReasonable.CheckThrowing();
            IsHeaderSectionHeaderOffsetValid.CheckThrowing();
            IsHeaderSectionHeaderEntrySizeValid.CheckThrowing();

            List<ELFSection> sections = new List<ELFSection>();
            for (uint i = 0; i < Header.SectionHeaderCount; i++)
            {
                sections.Add(new ELFSection(this, DataSourceReader, _position, _position + Header.SectionHeaderOffset + i * Header.SectionHeaderEntrySize));
            }
            return sections.ToArray();
        }

        private Reader CreateVirtualAddressReader()
        {
            if (_isDataSourceVirtualAddressSpace)
                return DataSourceReader;
            else
                return DataSourceReader.WithAddressSpace(new ELFVirtualAddressSpace(Segments));
        }

        private byte[] ReadBuildId()
        {
            byte[] buildId = null;

            if (Header.ProgramHeaderOffset > 0 && Header.ProgramHeaderEntrySize > 0 && Header.ProgramHeaderCount > 0)
            {
                foreach (ELFProgramSegment segment in Segments)
                {
                    if (segment.Header.Type == ELFProgramHeaderType.Note)
                    {
                        buildId = ReadBuildIdNote(segment.Contents);
                        if (buildId != null)
                        {
                            break;
                        }
                    }
                }
            }

            if (buildId == null)
            { 
                // Use sections to find build id if there isn't any program headers (i.e. some FreeBSD .dbg files)
                try
                {
                    foreach (ELFSection section in Sections)
                    {
                        if (section.Header.Type == ELFSectionHeaderType.Note)
                        {
                            if (string.Equals(section.Name, ".note.gnu.build-id"))
                            {
                                buildId = ReadBuildIdNote(section.Contents);
                                if (buildId != null)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is InvalidVirtualAddressException || ex is BadInputFormatException)
                {
                }
            }

            return buildId;
        }

        private byte[] ReadBuildIdNote(Reader noteReader)
        {
            if (noteReader != null)
            {
                var noteList = new ELFNoteList(noteReader);
                foreach (ELFNote note in noteList.Notes)
                {
                    ELFNoteType type = note.Header.Type;
                    if (type == ELFNoteType.PrpsInfo && note.Name.Equals("GNU"))
                    {
                        return note.Contents.Read(0, (uint)note.Contents.Length);
                    }
                }
            }
            return null;
        }

        private byte[] ReadSectionNameTable()
        {
            int nameTableIndex = Header.SectionHeaderStringIndex;
            if (Header.SectionHeaderOffset != 0 && Header.SectionHeaderCount > 0 && nameTableIndex != 0)
            {
                ELFSection nameTableSection = Sections[nameTableIndex];
                if (nameTableSection.Header.FileOffset > 0 && nameTableSection.Header.FileSize > 0)
                {
                    return nameTableSection.Contents.Read(0, (uint)nameTableSection.Contents.Length);
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
                    return Header.ProgramHeaderOffset < _reader.Length && 
                        Header.ProgramHeaderOffset + (ulong)(Header.ProgramHeaderEntrySize * Header.ProgramHeaderCount) <= _reader.Length;
                },
                IsHeaderProgramHeaderEntrySizeValid,
                Header.IsProgramHeaderCountReasonable);

            }
        }

        public ValidationRule IsHeaderProgramHeaderEntrySizeValid
        {
            get { return new ValidationRule("ELF Header ProgramHeaderEntrySize is invalid", () => Header.ProgramHeaderEntrySize == DataSourceReader.SizeOf<ELFProgramHeader>()); }
        }

        public ValidationRule IsHeaderSectionHeaderOffsetValid
        {
            get
            {
                return new ValidationRule("ELF Header SectionHeaderOffset is invalid or elf file is incomplete", () => 
                {
                    return Header.SectionHeaderOffset < _reader.Length &&
                        Header.SectionHeaderOffset + (ulong)(Header.SectionHeaderEntrySize * Header.SectionHeaderCount) <= _reader.Length;
                },
                IsHeaderSectionHeaderEntrySizeValid,
                Header.IsSectionHeaderCountReasonable);
            }
        }

        public ValidationRule IsHeaderSectionHeaderEntrySizeValid
        {
            get { return new ValidationRule("ELF Header SectionHeaderEntrySize is invalid", () => Header.SectionHeaderEntrySize == DataSourceReader.SizeOf<ELFSectionHeader>()); }
        }

        #endregion
    }

    public class ELFVirtualAddressSpace : IAddressSpace
    {
        private readonly ELFProgramSegment[] _segments;
        private readonly ulong _length;

        public ELFVirtualAddressSpace(IEnumerable<ELFProgramSegment> segments)
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
                // FileSize == 0 means the segment isn't backed by any data
                if (header.FileSize > 0 && header.VirtualAddress <= position && position + count <= header.VirtualAddress + header.VirtualSize)
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

            throw new InvalidVirtualAddressException(string.Format("Virtual address range is not mapped {0:X16} {1}", position, count));
        }
    }

    public class ELFProgramSegment
    {
        private readonly Reader _dataSourceReader;
        private readonly ulong _programHeaderOffset;
        private readonly Lazy<ELFProgramHeader> _header;
        private readonly Lazy<Reader> _contents;

        public ELFProgramSegment(Reader dataSourceReader, ulong elfOffset, ulong programHeaderOffset, bool isDataSourceVirtualAddressSpace)
        {
            _dataSourceReader = dataSourceReader;
            _programHeaderOffset = programHeaderOffset;
            _header = new Lazy<ELFProgramHeader>(() => _dataSourceReader.Read<ELFProgramHeader>(_programHeaderOffset));
            //TODO: validate p_vaddr, p_offset, p_filesz
            if (isDataSourceVirtualAddressSpace && _header.Value.Type == ELFProgramHeaderType.Load)
            {
                _contents = new Lazy<Reader>(() => _dataSourceReader.WithRelativeAddressSpace(Header.VirtualAddress, Header.VirtualSize));
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
                return "Segment@[" + Header.VirtualAddress.ToString() + "-" + (Header.VirtualAddress + Header.VirtualSize).ToString("x") + ")";
            }
            else
            {
                return "SegmentHeader@0x" + _programHeaderOffset.ToString("x");
            }
        }
    }

    public class ELFSection
    {
        private readonly ELFFile _elfFile;
        private readonly Reader _dataSourceReader;
        private readonly Lazy<ELFSectionHeader> _header;
        private readonly Lazy<string> _name;
        private readonly Lazy<Reader> _contents;

        private static readonly ASCIIEncoding _decoder = new ASCIIEncoding();

        public ELFSection(ELFFile elfFile, Reader dataSourceReader, ulong elfOffset, ulong sectionHeaderOffset)
        {
            _elfFile = elfFile;
            _dataSourceReader = dataSourceReader;
            _header = new Lazy<ELFSectionHeader>(() => _dataSourceReader.Read<ELFSectionHeader>(sectionHeaderOffset));
            _name = new Lazy<string>(ReadName);
            _contents = new Lazy<Reader>(() => _dataSourceReader.WithRelativeAddressSpace(elfOffset + Header.FileOffset, Header.FileSize));
        }

        public ELFSectionHeader Header { get { return _header.Value; } }
        public string Name { get { return _name.Value; } }
        public Reader Contents { get { return _contents.Value; } }

        private string ReadName()
        {
            if (Header.Type == ELFSectionHeaderType.Null)
            {
                return "";
            }
            int index = (int)Header.NameIndex;
            if (index == 0)
            {
                return "";
            }
            byte[] sectionNameTable = _elfFile.SectionNameTable;
            if (sectionNameTable == null || sectionNameTable.Length == 0)
            {
                return "";
            }
            int count = 0;
            for (; (index + count) < sectionNameTable.Length; count++)
            {
                if (sectionNameTable[index + count] == 0)
                {
                    break;
                }
            }
            return _decoder.GetString(sectionNameTable, index, count);
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
