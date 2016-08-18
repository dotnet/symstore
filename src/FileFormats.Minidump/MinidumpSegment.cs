namespace FileFormats.Minidump
{
    public class MinidumpSegment
    {
        public ulong Rva { get; private set; }
        public ulong Size { get; private set; }
        public ulong StartOfMemoryRange { get; private set; }

        public bool Contains(ulong address)
        {
            return StartOfMemoryRange <= address && address < StartOfMemoryRange + Size;
        }

        internal static MinidumpSegment Create(MINIDUMP_MEMORY_DESCRIPTOR region)
        {
            MinidumpSegment result = new MinidumpSegment();
            result.Rva = region.Memory.Rva;
            result.Size = region.Memory.DataSize;
            result.StartOfMemoryRange = region.StartOfMemoryRange;

            return result;
        }

        internal static MinidumpSegment Create(MINIDUMP_MEMORY_DESCRIPTOR64 region, ulong rva)
        {
            MinidumpSegment result = new MinidumpSegment();
            result.Rva = rva;
            result.Size = region.DataSize;
            result.StartOfMemoryRange = region.StartOfMemoryRange;

            return result;
        }
    }
}