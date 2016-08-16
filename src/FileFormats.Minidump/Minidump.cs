using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FileFormats.CrashDump
{
    public class MinidumpLoadedImage
    {
        public ulong BaseOfImage { get; private set; }
        public uint SizeOfImage { get; private set; }
        public uint CheckSum { get; private set; }
        public uint TimeDateStamp { get; private set; }
        public uint ModuleNameRva { get; private set; }

        internal MinidumpLoadedImage(Minidump.MINIDUMP_MODULE module)
        {
            BaseOfImage = module.Baseofimage;
            SizeOfImage = module.SizeOfImage;
            CheckSum = module.CheckSum;
            TimeDateStamp = module.TimeDateStamp;
            ModuleNameRva = module.ModuleNameRva;

            // TODO:  Version info.
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
        private readonly Lazy<MinidumpLoadedImage[]> _loadedImages;

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
            _loadedImages = new Lazy<MinidumpLoadedImage[]>(GetLoadedImages);
        }

        public MinidumpLoadedImage[] LoadedImages { get { return _loadedImages.Value; } }


        public bool Is64Bit
        {
            get
            {
                var arch = _systemInfo.ProcessorArchitecture;
                return arch == ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ALPHA64 || arch == ProcessorArchitecture.PROCESSOR_ARCHITECTURE_AMD64 || arch == ProcessorArchitecture.PROCESSOR_ARCHITECTURE_IA64;
            }
        }

        private MinidumpLoadedImage[] GetLoadedImages()
        {
            if (_moduleListStream == -1)
                throw new BadInputFormatException("Minidump does not contain a ModuleStreamList in its directory.");
            
            MINIDUMP_MODULE[] modules = _dataSourceReader.ReadCountedArray<MINIDUMP_MODULE>(_directory[_moduleListStream].Rva);
            return modules.Select(module => new MinidumpLoadedImage(module)).ToArray();
        }


        #region Native Structures
        #pragma warning disable 0649

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
            public int Rva;
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
        #endregion
    }
}
