using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileFormats.Minidump
{
    public static class CrashDumpLayoutManagerExtensions
    {
        public static LayoutManager AddCrashDumpTypes(this LayoutManager layouts, bool isBigEndian, bool is64Bit)
        {
            return layouts
                     .AddPrimitives(isBigEndian)
                     .AddEnumTypes()
                     .AddSizeT(is64Bit ? 8 : 4)
                     .AddPointerTypes()
                     .AddNullTerminatedString()
                     .AddTStructTypes();
        }
    }

#pragma warning disable 0649
#pragma warning disable 0169

    internal class MINIDUMP_HEADER : TStruct
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

    internal class MINIDUMP_DIRECTORY : TStruct
    {
        public MINIDUMP_STREAM_TYPE StreamType;
        public uint DataSize;
        public uint Rva;
    }

    internal enum MINIDUMP_STREAM_TYPE
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


    internal class MINIDUMP_SYSTEM_INFO : TStruct
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

    internal enum ProcessorArchitecture : ushort
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
}
