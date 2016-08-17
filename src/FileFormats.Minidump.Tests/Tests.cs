using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using TestHelpers;
using Xunit;

namespace FileFormats.Minidump
{
    public class Tests
    {
        const string x86Dump = "TestBinaries/minidump_x86.dmp.gz";
        const string x64Dump = "TestBinaries/minidump_x64.dmp.gz";

        static Tests()
        {
            
        }

        [Fact]
        public void CheckLoadedModules()
        {
            using (Stream stream = GetCrashDump(x86Dump))
                CheckLoadedModules(stream);

            using (Stream stream = GetCrashDump(x64Dump))
                CheckLoadedModules(stream);
        }

        private static void CheckLoadedModules(Stream stream)
        {
            IAddressSpace sas = new StreamAddressSpace(stream);
            Minidump minidump = new Minidump(sas);

            var modules = minidump.LoadedImages;
            Assert.True(modules.Length > 0);
        }

        [Fact]
        public void CheckStartupMemoryRead()
        {
            using (Stream stream = GetCrashDump(x86Dump))
                CheckStartupMemoryRead(stream);

            using (Stream stream = GetCrashDump(x64Dump))
                CheckStartupMemoryRead(stream);
        }


        private static void CheckStartupMemoryRead(Stream stream)
        {
            IAddressSpace sas = new StreamAddressSpace(stream);
            MaxStreamReadHelper readHelper = new MaxStreamReadHelper(sas);

            Minidump minidump = new Minidump(readHelper);

            // We should have read the header of a minidump, so we cannot have read nothing.
            Assert.True(readHelper.Max > 0);

            // We should only read the header and not too far into the dump file, 1k should be plenty.
            Assert.True(readHelper.Max <= 1024);
        }

        private Stream GetCrashDump(string path)
        {
            MemoryStream ms = new MemoryStream();
            using (FileStream fs = File.OpenRead(path))
            using (GZipStream gs = new GZipStream(fs, CompressionMode.Decompress))
                gs.CopyTo(ms);
            return ms;
        }
    }
}
