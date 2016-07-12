// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileFormats.ELF
{
    public class ELFCoreFile
    {
        IAddressSpace _dataSource;
        ELFFile _elf;
        Lazy<ELFFileTable> _fileTable;
        Lazy<ELFLoadedImage[]> _images;

        public ELFCoreFile(IAddressSpace dataSource)
        {
            _dataSource = dataSource;
            _elf = new ELFFile(dataSource);
            _fileTable = new Lazy<ELFFileTable>(ReadFileTable);
            _images = new Lazy<ELFLoadedImage[]>(ReadLoadedImages);
        }

        public ELFFileTable FileTable { get { return _fileTable.Value; } }
        public ELFLoadedImage[] LoadedImages { get { return _images.Value; } }

        ELFFileTable ReadFileTable()
        {
            foreach (ELFSegment seg in _elf.Segments)
            {
                if (seg.Header.Type == ELFProgramHeaderType.Note)
                {
                    ELFNoteList noteList = new ELFNoteList(seg.Contents);
                    foreach (ELFNote note in noteList.Notes)
                    {
                        if (note.Header.Type == ELFNoteType.File)
                        {
                            return new ELFFileTable(note.Contents);
                        }
                    }
                }
            }

            throw new BadInputFormatException("No ELF file table found");
        }

        ELFLoadedImage[] ReadLoadedImages()
        {
            return FileTable.Files.Select(e => new ELFLoadedImage(new ELFFile(_elf.VirtualAddressReader.DataSource, e.LoadAddress, true), e)).ToArray();
        }
    }

    public class ELFLoadedImage
    {
        ELFFileTableEntry _entry;

        public ELFLoadedImage(ELFFile image, ELFFileTableEntry entry)
        {
            Image = image;
            _entry = entry;
        }

        public ulong LoadAddress { get { return _entry.LoadAddress; } }
        public string Path { get { return _entry.Path; } }
        public ELFFile Image { get; private set; }
    }

    public class ELFFileTableEntry
    {
        ELFFileTableEntryPointers _ptrs;

        public ELFFileTableEntry(string path, ELFFileTableEntryPointers ptrs)
        {
            Path = path;
            _ptrs = ptrs;
        }

        public ulong LoadAddress { get { return _ptrs.Start; } }
        public string Path { get; private set; }
    }

    public class ELFFileTable
    {
        Reader _noteReader;
        Lazy<IEnumerable<ELFFileTableEntry>> _files;

        public ELFFileTable(Reader noteReader)
        {
            _noteReader = noteReader;
            _files = new Lazy<IEnumerable<ELFFileTableEntry>>(ReadFiles);
        }

        public IEnumerable<ELFFileTableEntry> Files { get { return _files.Value; } }

        private IEnumerable<ELFFileTableEntry> ReadFiles()
        {
            List<ELFFileTableEntry> files = new List<ELFFileTableEntry>();
            ulong readPosition = 0;
            ELFFileTableHeader header = _noteReader.Read<ELFFileTableHeader>(ref readPosition);

            //TODO: sanity check the entryCount
            ELFFileTableEntryPointers[] ptrs = _noteReader.ReadArray<ELFFileTableEntryPointers>(ref readPosition, (uint)(ulong)header.EntryCount);
            for (int i = 0; i < (int)(ulong)header.EntryCount; i++)
            {
                files.Add(new ELFFileTableEntry(_noteReader.Read<string>(ref readPosition), ptrs[i]));
            }
            return files;
        }
    }
}
