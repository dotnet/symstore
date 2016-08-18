using FileFormats.PE;
using System;
using System.Text;

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

        internal MinidumpLoadedImage(MINIDUMP_MODULE module, Reader virtualAddressReader, Reader reader)
        {
            BaseOfImage = module.Baseofimage;
            SizeOfImage = module.SizeOfImage;
            CheckSum = module.CheckSum;
            TimeDateStamp = module.TimeDateStamp;

            _peFile = new Lazy<PEFile>(() => new PEFile(virtualAddressReader.DataSource, BaseOfImage));
            _moduleName = new Lazy<string>(() => reader.ReadCountedString(module.ModuleNameRva, Encoding.Unicode));
        }
    }
}