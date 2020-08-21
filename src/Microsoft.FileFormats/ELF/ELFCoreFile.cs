// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.FileFormats.ELF
{
    public class ELFCoreFile
    {
        private readonly ELFFile _elf;
        private readonly Lazy<ELFFileTable> _fileTable;
        private readonly Lazy<ELFLoadedImage[]> _images;

        public ELFCoreFile(IAddressSpace dataSource)
        {
            _elf = new ELFFile(dataSource);
            _fileTable = new Lazy<ELFFileTable>(ReadFileTable);
            _images = new Lazy<ELFLoadedImage[]>(ReadLoadedImages);
        }

        public ELFFileTable FileTable { get { return _fileTable.Value; } }
        public ELFLoadedImage[] LoadedImages { get { return _images.Value; } }
        public IAddressSpace DataSource { get { return _elf.VirtualAddressReader.DataSource; } }

        public bool IsValid()
        {
            return _elf.IsValid() && _elf.Header.Type == ELFHeaderType.Core;
        }

        private ELFFileTable ReadFileTable()
        {
            foreach (ELFProgramSegment seg in _elf.Segments)
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

        private ELFLoadedImage[] ReadLoadedImages()
        {
            Dictionary<string, ELFFileTableEntry> normalizedFiles = new Dictionary<string, ELFFileTableEntry>();

            foreach (var fte in FileTable.Files.Where(fte => !fte.Path.StartsWith("/dev/zero") && !fte.Path.StartsWith("/run/shm")))
            {
                if (!normalizedFiles.ContainsKey(fte.Path) || fte.LoadAddress < normalizedFiles[fte.Path].LoadAddress)
                {
                    normalizedFiles[fte.Path] = fte;
                }
            }

            return normalizedFiles.Select(e => new ELFLoadedImage(new ELFFile(_elf.VirtualAddressReader.DataSource, e.Value.LoadAddress, true), e.Value)).ToArray();
        }
    }

    public class ELFLoadedImage
    {
        private readonly ELFFileTableEntry _entry;

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
        private readonly ELFFileTableEntryPointers _ptrs;

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
        private readonly Reader _noteReader;
        private readonly Lazy<IEnumerable<ELFFileTableEntry>> _files;

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
                string path = _noteReader.Read<string>(ref readPosition);

                // This substitution is for unloaded modules for which Linux appends " (deleted)" to the module name.
                path = path.Replace(" (deleted)", "");

                files.Add(new ELFFileTableEntry(path, ptrs[i]));
            }
            return files;
        }
    }
}
