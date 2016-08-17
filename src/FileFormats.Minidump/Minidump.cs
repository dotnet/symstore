using FileFormats.PE;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FileFormats.Minidump
{
    public class MinidumpLoadedImage
    {
        private readonly Lazy<PEFile> _peFile;
        private readonly Lazy<string> _moduleName;

        public ulong BaseOfImage { get; private set; }
        public uint SizeOfImage { get; private set; }
        public uint CheckSum { get; private set; }
        public uint TimeDateStamp { get; private set; }
        public string ModuleName { get { return _moduleName.Value; } }
        public PEFile Image { get { return _peFile.Value; } }

        internal MinidumpLoadedImage(Minidump.MINIDUMP_MODULE module, Reader virtualAddressReader, Reader reader)
        {
            BaseOfImage = module.Baseofimage;
            SizeOfImage = module.SizeOfImage;
            CheckSum = module.CheckSum;
            TimeDateStamp = module.TimeDateStamp;

            _peFile = new Lazy<PEFile>(() => new PEFile(virtualAddressReader.DataSource, BaseOfImage));
            _moduleName = new Lazy<string>(() => reader.Read<string>(module.ModuleNameRva));
        }
    }

    public class MinidumpSegment
    {
        public ulong Rva { get; private set; }
        public ulong Size { get; private set; }
        public ulong StartOfMemoryRange { get; private set; }

        public bool Contains(ulong address)
        {
            return StartOfMemoryRange <= address && address < StartOfMemoryRange + Size;
        }

        internal static MinidumpSegment Create(Minidump.MINIDUMP_MEMORY_DESCRIPTOR region)
        {
            MinidumpSegment result = new MinidumpSegment();
            result.Rva = region.Memory.Rva;
            result.Size = region.Memory.DataSize;
            result.StartOfMemoryRange = region.StartOfMemoryRange;

            return result;
        }

        internal static MinidumpSegment Create(Minidump.MINIDUMP_MEMORY_DESCRIPTOR64 region, ulong rva)
        {
            MinidumpSegment result = new MinidumpSegment();
            result.Rva = rva;
            result.Size = region.DataSize;
            result.StartOfMemoryRange = region.StartOfMemoryRange;

            return result;
        }
    }

    public class Minidump
    {
        private readonly ulong _position;
        private readonly IAddressSpace _dataSource;
        private readonly Reader _dataSourceReader;
        private readonly MINIDUMP_HEADER _header;
        private readonly MINIDUMP_DIRECTORY[] _directory;
        private readonly MINIDUMP_SYSTEM_INFO _systemInfo;
        private readonly int _moduleListStream = -1;
        private readonly Lazy<List<MinidumpLoadedImage>> _loadedImages;
        private readonly Lazy<List<MinidumpSegment>> _memoryRanges;
        private Lazy<Reader> _virtualAddressReader;

        public Minidump(IAddressSpace addressSpace, ulong position = 0)
        {
            _dataSource = addressSpace;
            _position = position;

            Reader headerReader = new Reader(_dataSource);
            _header = headerReader.Read<MINIDUMP_HEADER>(_position);
            _header.IsSignatureValid.CheckThrowing();

            int systemIndex = -1;
            _directory = new MINIDUMP_DIRECTORY[_header.NumberOfStreams];
            ulong streamPos = _position + _header.StreamDirectoryRva;
            for (int i = 0; i < _directory.Length; i++)
            {
                _directory[i] = headerReader.Read<MINIDUMP_DIRECTORY>(ref streamPos);

                var streamType = _directory[i].StreamType;
                if (streamType == MINIDUMP_STREAM_TYPE.SystemInfoStream)
                {
                    Debug.Assert(systemIndex == -1);
                    systemIndex = i;
                }
                else if (streamType == MINIDUMP_STREAM_TYPE.ModuleListStream)
                {
                    Debug.Assert(_moduleListStream == -1);
                    _moduleListStream = i;
                }
            }

            if (systemIndex == -1)
                throw new BadInputFormatException("Minidump does not contain a MINIDUMP_SYSTEM_INFO stream");

            _systemInfo = headerReader.Read<MINIDUMP_SYSTEM_INFO>(_position + _directory[systemIndex].Rva);

            _dataSourceReader = new Reader(_dataSource, new LayoutManager().AddCrashDumpTypes(false, Is64Bit));
            _loadedImages = new Lazy<List<MinidumpLoadedImage>>(CreateLoadedImageList);
            _memoryRanges = new Lazy<List<MinidumpSegment>>(CreateSegmentList);
            _virtualAddressReader = new Lazy<Reader>(CreateVirtualAddressReader);
        }

        public Reader DataSourceReader { get { return _dataSourceReader; } }
        public Reader VirtualAddressReader { get { return _virtualAddressReader.Value; } }
        public ReadOnlyCollection<MinidumpLoadedImage> LoadedImages { get { return _loadedImages.Value.AsReadOnly(); } }
        public ReadOnlyCollection<MinidumpSegment> Segments { get { return _memoryRanges.Value.AsReadOnly(); } }

        public bool Is64Bit
        {
            get
            {
                var arch = _systemInfo.ProcessorArchitecture;
                return arch == ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ALPHA64 || arch == ProcessorArchitecture.PROCESSOR_ARCHITECTURE_AMD64 || arch == ProcessorArchitecture.PROCESSOR_ARCHITECTURE_IA64;
            }
        }

        private Reader CreateVirtualAddressReader()
        {
            return _dataSourceReader.WithAddressSpace(new MinidumpVirtualAddressSpace(Segments, _dataSource));
        }

        private List<MinidumpLoadedImage> CreateLoadedImageList()
        {
            if (_moduleListStream == -1)
                throw new BadInputFormatException("Minidump does not contain a ModuleStreamList in its directory.");
            
            MINIDUMP_MODULE[] modules = _dataSourceReader.ReadCountedArray<MINIDUMP_MODULE>(_directory[_moduleListStream].Rva);
            return new List<MinidumpLoadedImage>(modules.Select(module => new MinidumpLoadedImage(module, VirtualAddressReader, DataSourceReader)));
        }

        private List<MinidumpSegment> CreateSegmentList()
        {
            List<MinidumpSegment> ranges = new List<MinidumpSegment>();

            foreach (MINIDUMP_DIRECTORY item in _directory)
            {
                if (item.StreamType == MINIDUMP_STREAM_TYPE.MemoryListStream)
                {
                    MINIDUMP_MEMORY_DESCRIPTOR[] memoryRegions = _dataSourceReader.ReadCountedArray<MINIDUMP_MEMORY_DESCRIPTOR>(item.Rva);

                    foreach (var region in memoryRegions)
                    {
                        MinidumpSegment range = MinidumpSegment.Create(region);
                        ranges.Add(range);
                    }

                }
                else if (item.StreamType == MINIDUMP_STREAM_TYPE.Memory64ListStream)
                {
                    ulong position = item.Rva;
                    ulong count = _dataSourceReader.Read<ulong>(ref position);
                    ulong rva = _dataSourceReader.Read<ulong>(ref position);

                    MINIDUMP_MEMORY_DESCRIPTOR64[] memoryRegions = _dataSourceReader.ReadArray<MINIDUMP_MEMORY_DESCRIPTOR64>(position, checked((uint)count));
                    foreach (var region in memoryRegions)
                    {
                        MinidumpSegment range = MinidumpSegment.Create(region, rva);
                        ranges.Add(range);

                        rva += region.DataSize;
                    }
                }
            }

            ranges.Sort((MinidumpSegment a, MinidumpSegment b) => a.StartOfMemoryRange.CompareTo(b.StartOfMemoryRange));
            return ranges;
        }



        #region Native Structures
#pragma warning disable 0649
#pragma warning disable 0169

        private class MINIDUMP_HEADER : TStruct
        {
            public const int MINIDUMP_VERSION = 0x504d444d;

            public uint Signature;
            public uint Version;
            public uint NumberOfStreams;
            public uint StreamDirectoryRva;
            public uint CheckSum;
            public uint TimeDateStamp;
            public ulong Flags;

            public ValidationRule IsSignatureValid
            {
                get
                {
                    return new ValidationRule("Invalid minidump header signature", () =>
                    {
                        return Signature == MINIDUMP_VERSION;
                    });
                }
            }
        }
        
        private class MINIDUMP_DIRECTORY : TStruct
        {
            public MINIDUMP_STREAM_TYPE StreamType;
            public uint DataSize;
            public uint Rva;
        }

        private enum MINIDUMP_STREAM_TYPE
        {
            UnusedStream = 0,
            ReservedStream0 = 1,
            ReservedStream1 = 2,
            ThreadListStream = 3,
            ModuleListStream = 4,
            MemoryListStream = 5,
            ExceptionStream = 6,
            SystemInfoStream = 7,
            ThreadExListStream = 8,
            Memory64ListStream = 9,
            CommentStreamA = 10,
            CommentStreamW = 11,
            HandleDataStream = 12,
            FunctionTableStream = 13,
            UnloadedModuleListStream = 14,
            MiscInfoStream = 15,
            MemoryInfoListStream = 16,
            ThreadInfoListStream = 17,
            LastReservedStream = 0xffff,
        }

        
        private class MINIDUMP_SYSTEM_INFO : TStruct
        {
            public ProcessorArchitecture ProcessorArchitecture;
            public ushort ProcessorLevel;
            public ushort ProcessorRevision;
            public byte NumberOfProcessors;
            public byte ProductType;
            public uint MajorVersion;
            public uint MinorVersion;
            public uint BuildNumber;
            public uint PlatformId;
            public uint CSDVersionRva;
        }

        private enum ProcessorArchitecture : ushort
        {
            PROCESSOR_ARCHITECTURE_INTEL = 0,
            PROCESSOR_ARCHITECTURE_MIPS = 1,
            PROCESSOR_ARCHITECTURE_ALPHA = 2,
            PROCESSOR_ARCHITECTURE_PPC = 3,
            PROCESSOR_ARCHITECTURE_SHX = 4,
            PROCESSOR_ARCHITECTURE_ARM = 5,
            PROCESSOR_ARCHITECTURE_IA64 = 6,
            PROCESSOR_ARCHITECTURE_ALPHA64 = 7,
            PROCESSOR_ARCHITECTURE_MSIL = 8,
            PROCESSOR_ARCHITECTURE_AMD64 = 9,
            PROCESSOR_ARCHITECTURE_IA32_ON_WIN64 = 10,
        }
        
        internal sealed class VS_FIXEDFILEINFO : TStruct
        {
            public uint dwSignature;            /* e.g. 0xfeef04bd */
            public uint dwStrucVersion;         /* e.g. 0x00000042 = "0.42" */
            public uint dwFileVersionMS;        /* e.g. 0x00030075 = "3.75" */
            public uint dwFileVersionLS;        /* e.g. 0x00000031 = "0.31" */
            public uint dwProductVersionMS;     /* e.g. 0x00030010 = "3.10" */
            public uint dwProductVersionLS;     /* e.g. 0x00000031 = "0.31" */
            public uint dwFileFlagsMask;        /* = 0x3F for version "0.42" */
            public uint dwFileFlags;            /* e.g. VFF_DEBUG | VFF_PRERELEASE */
            public uint dwFileOS;               /* e.g. VOS_DOS_WINDOWS16 */
            public uint dwFileType;             /* e.g. VFT_DRIVER */
            public uint dwFileSubtype;          /* e.g. VFT2_DRV_KEYBOARD */

            // Timestamps would be useful, but they're generally missing (0).
            public uint dwFileDateMS;           /* e.g. 0 */
            public uint dwFileDateLS;           /* e.g. 0 */
        }

        
        internal sealed class MINIDUMP_LOCATION_DESCRIPTOR : TStruct
        {
            public uint DataSize;
            public uint Rva;
        }

        [TStructPack(4)]
        internal sealed class MINIDUMP_MODULE : TStruct
        {
            public ulong Baseofimage;
            public uint SizeOfImage;
            public uint CheckSum;
            public uint TimeDateStamp;
            public uint ModuleNameRva;
            public VS_FIXEDFILEINFO VersionInfo;
            public MINIDUMP_LOCATION_DESCRIPTOR CvRecord;
            public MINIDUMP_LOCATION_DESCRIPTOR MiscRecord;
            private ulong _reserved0;
            private ulong _reserved1;
        }

        internal sealed class MINIDUMP_MEMORY_DESCRIPTOR : TStruct
        {
            public ulong StartOfMemoryRange;
            public MINIDUMP_LOCATION_DESCRIPTOR Memory;

        }

        internal sealed class MINIDUMP_MEMORY_DESCRIPTOR64 : TStruct
        {
            public ulong StartOfMemoryRange;
            public ulong DataSize;
        }
        #endregion
    }
}
