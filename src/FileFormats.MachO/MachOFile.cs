// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using FileFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileFormats.MachO
{
    public class MachOFatFile
    {
        IAddressSpace _dataSource;
        Lazy<MachFatHeaderMagic> _headerMagic;
        Lazy<Reader> _headerReader;
        Lazy<MachFatHeader> _header;
        Lazy<MachFatArch[]> _arches;
        Lazy<MachOFile[]> _archSpecificFiles;

        public MachOFatFile(IAddressSpace dataSource)
        {
            _dataSource = dataSource;
            _headerMagic = new Lazy<MachFatHeaderMagic>(() => new Reader(_dataSource).Read<MachFatHeaderMagic>(0));
            _headerReader = new Lazy<Reader>(() => new Reader(_dataSource, new LayoutManager().AddMachFatHeaderTypes(IsBigEndian)));
            _header = new Lazy<MachFatHeader>(() => _headerReader.Value.Read<MachFatHeader>(0));
            _arches = new Lazy<MachFatArch[]>(ReadArches);
            _archSpecificFiles = new Lazy<MachOFile[]>(ReadArchSpecificFiles);
        }

        public MachFatHeaderMagic HeaderMagic {  get { return _headerMagic.Value; } }
        public MachFatHeader Header {  get { return _header.Value; } }
        public MachFatArch[] Arches {  get { return _arches.Value; } }
        public MachOFile[] ArchSpecificFiles { get { return _archSpecificFiles.Value; } }
        
        public bool IsBigEndian
        {
            get
            {
                HeaderMagic.IsMagicValid.CheckThrowing();
                return HeaderMagic.Magic == MachFatHeaderMagicKind.BigEndian;
            }
        }

        MachFatArch[] ReadArches()
        {
            Header.IsCountFatArchesReasonable.CheckThrowing();
            ulong position = _headerReader.Value.SizeOf<MachFatHeader>();
            return _headerReader.Value.ReadArray<MachFatArch>(position, Header.CountFatArches);
        }

        MachOFile[] ReadArchSpecificFiles()
        {
            return Arches.Select(a => new MachOFile(new RelativeAddressSpace(_dataSource, a.Offset, a.Size))).ToArray();
        }
    }

    public class MachOFile
    {
        IAddressSpace _dataSource;
        ulong _position;
        bool _dataSourceIsVirtualAddressSpace;
        Lazy<MachHeaderMagic> _headerMagic;
        Lazy<Reader> _dataSourceReader;
        Lazy<MachHeader> _header;
        Lazy<Tuple<MachLoadCommand, ulong>[]> _loadCommands;
        Lazy<MachSegment[]> _segments;
        Lazy<MachSection[]> _sections;
        Lazy<Reader> _virtualAddressReader;
        Lazy<Reader> _physicalAddressReader;
        Lazy<byte[]> _uuid;
        Lazy<MachSymtab> _symtab;

        public MachOFile(IAddressSpace dataSource, ulong position = 0, bool dataSourceIsVirtualAddressSpace = false)
        {
            _dataSource = dataSource;
            _position = position;
            _dataSourceIsVirtualAddressSpace = dataSourceIsVirtualAddressSpace;
            _headerMagic = new Lazy<MachHeaderMagic>(() => new Reader(_dataSource).Read<MachHeaderMagic>(_position));
            _dataSourceReader = new Lazy<Reader>(CreateDataSourceReader);
            _header = new Lazy<MachHeader>(() => DataSourceReader.Read<MachHeader>(_position));
            _loadCommands = new Lazy<Tuple<MachLoadCommand, ulong>[]>(ReadLoadCommands);
            _segments = new Lazy<MachSegment[]>(ReadSegments);
            _sections = new Lazy<MachSection[]>(() => Segments.SelectMany(seg => seg.Sections).ToArray());
            _virtualAddressReader = new Lazy<Reader>(CreateVirtualReader);
            _physicalAddressReader = new Lazy<Reader>(CreatePhysicalReader);
            _uuid = new Lazy<byte[]>(ReadUuid);
            _symtab = new Lazy<MachSymtab>(ReadSymtab);
        }

        public MachHeaderMagic HeaderMagic { get { return _headerMagic.Value; } }
        
        public MachHeader Header { get { return _header.Value; } }
        public byte[] Uuid { get { return _uuid.Value; } }
        public MachSegment[] Segments { get { return _segments.Value; } }
        public MachSection[] Sections {  get { return _sections.Value; } }
        public Reader VirtualAddressReader { get { return _virtualAddressReader.Value; } }
        public Reader PhysicalAddressReader {  get { return _physicalAddressReader.Value; } }
        public MachSymtab Symtab { get { return _symtab.Value; } }

        Reader DataSourceReader { get { return _dataSourceReader.Value; } }

        public bool IsBigEndian
        {
            get
            {
                HeaderMagic.IsMagicValid.CheckThrowing();
                return (HeaderMagic.Magic == MachHeaderMagicType.BigEndian32Bit ||
                        HeaderMagic.Magic == MachHeaderMagicType.BigEndian64Bit);
            }
        }

        public bool Is64Bit
        {
            get
            {
                HeaderMagic.IsMagicValid.CheckThrowing();
                return (HeaderMagic.Magic == MachHeaderMagicType.LittleEndian64Bit ||
                        HeaderMagic.Magic == MachHeaderMagicType.BigEndian64Bit);
            }
        }

        public ulong PreferredVMBaseAddress
        {
            get
            {
                IsAtLeastOneSegmentAtFileOffsetZero.CheckThrowing();
                return Segments.Where(s => s.LoadCommand.FileOffset == 0 && s.LoadCommand.FileSize != 0).First().
                    LoadCommand.VMAddress;
            }
        }

        public ulong LoadAddress
        {
            get
            {
                if(_dataSourceIsVirtualAddressSpace)
                {
                    return _position;
                }
                else
                {
                    return PreferredVMBaseAddress;
                }
            }
        }

        Reader CreateDataSourceReader()
        {
            return new Reader(_dataSource, new LayoutManager().AddMachTypes(IsBigEndian, Is64Bit));
        }

        Reader CreateVirtualReader()
        {
            if (_dataSourceIsVirtualAddressSpace)
            {
                return DataSourceReader;
            }
            else
            {
                return DataSourceReader.WithAddressSpace(new MachVirtualAddressSpace(Segments));
            }
        }

        Reader CreatePhysicalReader()
        {
            if (!_dataSourceIsVirtualAddressSpace)
            {
                return DataSourceReader;
            }
            else
            {
                return DataSourceReader.WithAddressSpace(new MachPhysicalAddressSpace(_dataSource, _position, PreferredVMBaseAddress, Segments));
            }
        }

        Tuple<MachLoadCommand, ulong>[] ReadLoadCommands()
        {
            Header.IsNumberCommandsReasonable.CheckThrowing();
            ulong position = _position + DataSourceReader.SizeOf<MachHeader>();
            //TODO: do this more cleanly
            if (Is64Bit)
            {
                position += 4; // the 64 bit version has an extra padding field to align at an
                               // 8 byte boundary
            }
            List<Tuple<MachLoadCommand, ulong>> cmds = new List<Tuple<MachLoadCommand, ulong>>();
            for (uint i = 0; i < Header.NumberCommands; i++)
            {
                MachLoadCommand cmd = DataSourceReader.Read<MachLoadCommand>(position);
                cmd.IsCmdSizeReasonable.CheckThrowing();
                cmds.Add(new Tuple<MachLoadCommand, ulong>(cmd, position));
                position += cmd.CommandSize;
            }

            return cmds.ToArray();
        }

        byte[] ReadUuid()
        {
            IsAtLeastOneUuidLoadCommand.CheckThrowing();
            IsAtMostOneUuidLoadCommand.CheckThrowing();
            Tuple<MachLoadCommand, ulong> cmdAndPos = _loadCommands.Value.Where(c => c.Item1.Command == LoadCommandType.Uuid).First();
            MachUuidLoadCommand uuidCmd = DataSourceReader.Read<MachUuidLoadCommand>(cmdAndPos.Item2);
            uuidCmd.IsCommandSizeValid.CheckThrowing();
            return uuidCmd.Uuid;
        }

        MachSegment[] ReadSegments()
        {
            List<MachSegment> segs = new List<MachSegment>();
            foreach (Tuple<MachLoadCommand, ulong> cmdAndPos in _loadCommands.Value)
            {
                LoadCommandType segType = Is64Bit ? LoadCommandType.Segment64 : LoadCommandType.Segment;
                if (cmdAndPos.Item1.Command != segType)
                {
                    continue;
                }
                MachSegment seg = new MachSegment(DataSourceReader, cmdAndPos.Item2, _dataSourceIsVirtualAddressSpace);
                segs.Add(seg);
            }

            return segs.ToArray();
        }

        MachSymtab ReadSymtab()
        {
            IsAtLeastOneSymtabLoadCommand.CheckThrowing();
            IsAtMostOneSymtabLoadCommand.CheckThrowing();
            foreach (Tuple<MachLoadCommand, ulong> cmdAndPos in _loadCommands.Value)
            {
                if (cmdAndPos.Item1.Command != LoadCommandType.Symtab)
                {
                    continue;
                }
                return new MachSymtab(DataSourceReader, cmdAndPos.Item2, PhysicalAddressReader);
            }

            return null;
        }

        #region Validation Rules
        public ValidationRule IsAtMostOneUuidLoadCommand
        {
            get
            {
                return new ValidationRule("Mach load command sequence has too many uuid elements",
                                          () => _loadCommands.Value.Count(c => c.Item1.Command == LoadCommandType.Uuid) <= 1);
            }
        }
        public ValidationRule IsAtLeastOneUuidLoadCommand
        {
            get
            {
                return new ValidationRule("Mach load command sequence has no uuid elements",
                                          () => _loadCommands.Value.Any(c => c.Item1.Command == LoadCommandType.Uuid));
            }
        }
        public ValidationRule IsAtMostOneSymtabLoadCommand
        {
            get
            {
                return new ValidationRule("Mach load command sequence has too many symtab elements",
                                          () => _loadCommands.Value.Count(c => c.Item1.Command == LoadCommandType.Symtab) <= 1);
            }
        }
        public ValidationRule IsAtLeastOneSymtabLoadCommand
        {
            get
            {
                return new ValidationRule("Mach load command sequence has no symtab elements",
                                          () => _loadCommands.Value.Any(c => c.Item1.Command == LoadCommandType.Symtab));
            }
        }
        public ValidationRule IsAtLeastOneSegmentAtFileOffsetZero
        {
            get
            {
                return new ValidationRule("Mach load command sequence has no segments which contain file offset zero",
                                          () => Segments.Where(s => s.LoadCommand.FileOffset == 0 &&
                                                                    s.LoadCommand.FileSize != 0).Any());
            }
        }
        #endregion
    }

    public class MachSegment
    {
        Reader _dataSourceReader;
        ulong _position;
        bool _readerIsVirtualAddressSpace;
        Lazy<MachSegmentLoadCommand> _loadCommand;
        Lazy<MachSection[]> _sections;
        Lazy<Reader> _physicalContents;
        Lazy<Reader> _virtualContents;

        public MachSegment(Reader machReader, ulong position, bool readerIsVirtualAddressSpace = false)
        {
            _dataSourceReader = machReader;
            _position = position;
            _readerIsVirtualAddressSpace = readerIsVirtualAddressSpace;
            _loadCommand = new Lazy<MachSegmentLoadCommand>(() => _dataSourceReader.Read<MachSegmentLoadCommand>(_position));
            _sections = new Lazy<MachSection[]>(ReadSections);
            _physicalContents = new Lazy<Reader>(CreatePhysicalSegmentAddressSpace);
            _virtualContents = new Lazy<Reader>(CreateVirtualSegmentAddressSpace);
        }

        public MachSegmentLoadCommand LoadCommand {  get { return _loadCommand.Value; } }
        public IEnumerable<MachSection> Sections {  get { return _sections.Value; } }
        public Reader PhysicalContents { get { return _physicalContents.Value; } }
        public Reader VirtualContents {  get { return _virtualContents.Value; } }

        private MachSection[] ReadSections()
        {
            ulong sectionStartOffset = _position + _dataSourceReader.SizeOf<MachSegmentLoadCommand>();
            return _dataSourceReader.ReadArray<MachSection>(sectionStartOffset, _loadCommand.Value.CountSections);
        }

        Reader CreatePhysicalSegmentAddressSpace()
        {
            if (!_readerIsVirtualAddressSpace)
            {
                return _dataSourceReader.WithRelativeAddressSpace(LoadCommand.FileOffset, LoadCommand.FileSize, 0);
            }
            else
            {
                return _dataSourceReader.WithRelativeAddressSpace(LoadCommand.VMAddress, LoadCommand.FileSize,
                                                            (long)(LoadCommand.FileOffset - LoadCommand.VMAddress));
            }
        }

        Reader CreateVirtualSegmentAddressSpace()
        {
            if(_readerIsVirtualAddressSpace)
            {
                return _dataSourceReader.WithRelativeAddressSpace(LoadCommand.VMAddress, LoadCommand.VMSize, 0);
            }
            else
            {
                return _dataSourceReader.WithAddressSpace(
                    new PiecewiseAddressSpace(
                        new PiecewiseAddressSpaceRange()
                        {
                            AddressSpace = new RelativeAddressSpace(_dataSourceReader.DataSource, LoadCommand.FileOffset, LoadCommand.FileSize,
                                                                             (long)(LoadCommand.VMAddress - LoadCommand.FileOffset)),
                            Start = LoadCommand.VMAddress,
                            Length = LoadCommand.FileSize
                        },
                        new PiecewiseAddressSpaceRange()
                        {
                            AddressSpace = new ZeroAddressSpace(LoadCommand.VMAddress + LoadCommand.VMSize),
                            Start = LoadCommand.VMAddress + LoadCommand.FileSize,
                            Length = LoadCommand.VMSize - LoadCommand.FileSize
                        }));
            }
        }
    }


    public class MachVirtualAddressSpace : PiecewiseAddressSpace
    {
        public MachVirtualAddressSpace(IEnumerable<MachSegment> segments) : base( segments.Select(s => ToRange(s)).ToArray())
        {
        }

        static PiecewiseAddressSpaceRange ToRange(MachSegment segment)
        {
            return new PiecewiseAddressSpaceRange()
            {
                AddressSpace = segment.VirtualContents.DataSource,
                Start = segment.LoadCommand.VMAddress,
                Length = segment.LoadCommand.VMSize
            };
        }
    }

    public class MachPhysicalAddressSpace : PiecewiseAddressSpace
    {
        public MachPhysicalAddressSpace(IAddressSpace virtualAddressSpace, ulong baseLoadAddress, ulong preferredVMBaseAddress, IEnumerable<MachSegment> segments) :
            base( segments.Select(s => ToRange(virtualAddressSpace, baseLoadAddress, preferredVMBaseAddress, s)).ToArray())
        {
        }

        static PiecewiseAddressSpaceRange ToRange(IAddressSpace virtualAddressSpace, ulong baseLoadAddress, ulong preferredVMBaseAddress, MachSegment segment)
        {
            ulong actualSegmentLoadAddress = segment.LoadCommand.VMAddress - preferredVMBaseAddress + baseLoadAddress;
            return new PiecewiseAddressSpaceRange()
            {
                AddressSpace = new RelativeAddressSpace(virtualAddressSpace, actualSegmentLoadAddress, segment.LoadCommand.FileSize,
                                                            (long)(segment.LoadCommand.FileOffset - actualSegmentLoadAddress)),
                Start = segment.LoadCommand.FileOffset,
                Length = segment.LoadCommand.FileSize
            };
        }
    }

    public class MachSymbol
    {
        public string Name;
        public ulong Value { get { return Raw.Value; } }
        public NList Raw;

        public override string ToString()
        {
            return Name + "@0x" + Value.ToString("x");
        }
    }

    public class MachSymtab
    {
        Reader _machReader;
        ulong _position;
        Reader _physicalAddressSpace;
        Lazy<MachSymtabLoadCommand> _loadCommand;
        Lazy<MachSymbol[]> _symbols;
        

        public MachSymtab(Reader machReader, ulong position, Reader physicalAddressSpace)
        {
            _machReader = machReader;
            _position = position;
            _physicalAddressSpace = physicalAddressSpace;
            _loadCommand = new Lazy<MachSymtabLoadCommand>(() => _machReader.Read<MachSymtabLoadCommand>(_position));
            _symbols = new Lazy<MachSymbol[]>(ReadSymbols);
        }

        public MachSymtabLoadCommand LoadCommand { get { return _loadCommand.Value; } }
        public IEnumerable<MachSymbol> Symbols {  get { return _symbols.Value; } }

        MachSymbol[] ReadSymbols()
        {
            LoadCommand.IsNSymsReasonable.CheckThrowing();
            NList[] nlists = _physicalAddressSpace.ReadArray<NList>(LoadCommand.SymOffset, LoadCommand.SymCount);
            Reader stringReader = _physicalAddressSpace.WithRelativeAddressSpace(LoadCommand.StringOffset, LoadCommand.StringSize);
            return nlists.Select(n => new MachSymbol() { Name = stringReader.Read<string>(n.StringIndex), Raw = n }).ToArray();
        }
    }
}
