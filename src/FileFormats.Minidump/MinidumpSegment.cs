namespace FileFormats.Minidump
{
    /// <summary>
    /// Represents a segment of memory in the minidump's virtual address space.
    /// </summary>
    public class MinidumpSegment
    {
        /// <summary>
        /// The physical location in the minidump file where this memory segment resides.
        /// </summary>
        public ulong FileOffset { get; private set; }

        /// <summary>
        /// The base address of this chunk of virtual memory in the original process.
        /// </summary>
        public ulong VirtualAddress { get; private set; }

        /// <summary>
        /// The size of this chunk of memory.  Note that this is both the size of the physical
        /// memory in the minidump as well as the virtual memory in the original process.
        /// </summary>
        public ulong Size { get; private set; }

        /// <summary>
        /// Returns whether the given address is contained in this region of virtual memory.
        /// </summary>
        /// <param name="address">A virtual address in the orginal process's address space.</param>
        /// <returns>True if this segment contains the address, false otherwise.</returns>
        public bool Contains(ulong address)
        {
            return VirtualAddress <= address && address < VirtualAddress + Size;
        }

        internal static MinidumpSegment Create(MINIDUMP_MEMORY_DESCRIPTOR region)
        {
            MinidumpSegment result = new MinidumpSegment();
            result.FileOffset = region.Memory.Rva;
            result.Size = region.Memory.DataSize;
            result.VirtualAddress = region.StartOfMemoryRange;

            return result;
        }

        internal static MinidumpSegment Create(MINIDUMP_MEMORY_DESCRIPTOR64 region, ulong rva)
        {
            MinidumpSegment result = new MinidumpSegment();
            result.FileOffset = rva;
            result.Size = region.DataSize;
            result.VirtualAddress = region.StartOfMemoryRange;

            return result;
        }
    }
}