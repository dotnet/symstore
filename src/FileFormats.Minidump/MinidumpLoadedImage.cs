using FileFormats.PE;
using System;
using System.Text;

namespace FileFormats.Minidump
{
    public class MinidumpLoadedImage
    {
        private readonly Lazy<PEFile> _peFile;
        private readonly Lazy<string> _moduleName;

        /// <summary>
        /// The base address in the minidump's virtual address space that this image is mapped.
        /// </summary>
        public ulong BaseAddress { get; private set; }

        /// <summary>
        /// The checksum of this image.
        /// </summary>
        public uint CheckSum { get; private set; }

        /// <summary>
        /// The TimeDateStame of this image, as baked into the PE header.  This value is used
        /// for symbol sever requests to obtain a PE image.
        /// </summary>
        public uint TimeDateStamp { get; private set; }

        /// <summary>
        /// The compile time size of this PE image as it is baked into the PE header.  This
        /// value is used for simple server requests to obtain a PE image.
        /// </summary>
        public uint ImageSize { get; private set; }


        /// <summary>
        /// The full name of this module (including path it was orignally loaded from on disk).
        /// </summary>
        public string ModuleName { get { return _moduleName.Value; } }

        /// <summary>
        /// A PEFile representing this image.
        /// </summary>
        public PEFile Image { get { return _peFile.Value; } }

        internal MinidumpLoadedImage(MinidumpModule module, Reader virtualAddressReader, Reader reader)
        {
            BaseAddress = module.Baseofimage;
            ImageSize = module.SizeOfImage;
            CheckSum = module.CheckSum;
            TimeDateStamp = module.TimeDateStamp;

            _peFile = new Lazy<PEFile>(() => new PEFile(new RelativeAddressSpace(virtualAddressReader.DataSource, BaseAddress, virtualAddressReader.Length)));
            _moduleName = new Lazy<string>(() => reader.ReadCountedString(module.ModuleNameRva, Encoding.Unicode));
        }
    }
}
