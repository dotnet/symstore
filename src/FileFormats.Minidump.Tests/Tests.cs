using System.Linq;
using System.IO;
using System.IO.Compression;
using TestHelpers;
using Xunit;
using System.Collections.ObjectModel;
using System;

namespace FileFormats.Minidump
{
    public class Tests
    {
        const string x86Dump = "TestBinaries/minidump_x86.dmp.gz";
        const string x64Dump = "TestBinaries/minidump_x64.dmp.gz";
        
        [Fact]
        public void CheckNestedPEImages()
        {
            using (Stream stream = GetCrashDump(x86Dump))
                CheckNestedPEImages(GetMinidumpFromStream(stream));

            using (Stream stream = GetCrashDump(x64Dump))
                CheckNestedPEImages(GetMinidumpFromStream(stream));
        }

        private void CheckNestedPEImages(Minidump minidump)
        {
            foreach (var loadedImage in minidump.LoadedImages)
            {
                Assert.True(loadedImage.Image.HasValidDosSignature.Check());
                Assert.True(loadedImage.Image.HasValidPESignature.Check());
            }
        }

        [Fact]
        public void CheckMemoryRanges()
        {
            using (Stream stream = GetCrashDump(x86Dump))
                CheckMemoryRanges(GetMinidumpFromStream(stream));

            using (Stream stream = GetCrashDump(x64Dump))
                CheckMemoryRanges(GetMinidumpFromStream(stream));
        }

        private void CheckMemoryRanges(Minidump minidump)
        {
            ReadOnlyCollection<MinidumpLoadedImage> images = minidump.LoadedImages;
            ReadOnlyCollection<MinidumpSegment> memory = minidump.Segments;
            
            // Ensure that all of our images actually correspond to memory in the crash dump.  Note that our minidumps used
            // for this test are all full dumps with all memory (including images) in them.
            foreach (var image in images)
            {
                int count = memory.Where(m => m.StartOfMemoryRange <= image.BaseOfImage && image.BaseOfImage < m.StartOfMemoryRange + m.Size).Count();
                Assert.Equal(1, count);
                
                // Check the start of each image for the PE header 'MZ'
                byte[] header = minidump.VirtualAddressReader.Read(image.BaseOfImage, 2);
                Assert.Equal((byte)'M', header[0]);
                Assert.Equal((byte)'Z', header[1]);
            }
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
            Minidump minidump = GetMinidumpFromStream(stream);

            var modules = minidump.LoadedImages;
            Assert.True(modules.Count > 0);
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
        
        private static Minidump GetMinidumpFromStream(Stream stream)
        {
            IAddressSpace sas = new StreamAddressSpace(stream);
            Minidump minidump = new Minidump(sas);
            return minidump;
        }
    }
}
