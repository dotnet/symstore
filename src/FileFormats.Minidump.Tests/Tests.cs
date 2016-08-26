using System.Linq;
using System.IO;
using System.IO.Compression;
using TestHelpers;
using Xunit;
using System.Collections.ObjectModel;
using System;
using FileFormats.PE;

namespace FileFormats.Minidump
{
    public class Tests
    {
        const string x86Dump = "TestBinaries/minidump_x86.dmp.gz";
        const string x64Dump = "TestBinaries/minidump_x64.dmp.gz";

        readonly Guid x64ClrGuid = new Guid("e18d6461-eb4f-49a6-b418-e9af91007a21");
        readonly Guid x86ClrGuid = new Guid("df1e3528-29be-4d0e-9457-4c8ccfdc278a");
        const int ClrAge = 2;
        const string ClrPdb = "clr.pdb";


        [Fact]
        public void CheckIsMinidump()
        {
            using (Stream stream = GetCrashDump(x86Dump))
            {
                Assert.True(Minidump.IsValidMinidump(new StreamAddressSpace(stream)));
                Assert.False(Minidump.IsValidMinidump(new StreamAddressSpace(stream), 1));
            }

            using (Stream stream = GetCrashDump(x64Dump))
            {
                Assert.True(Minidump.IsValidMinidump(new StreamAddressSpace(stream)));
                Assert.False(Minidump.IsValidMinidump(new StreamAddressSpace(stream), 1));
            }

            // These are GZiped files, they should not be minidumps.
            using (FileStream stream = File.OpenRead(x86Dump))
                Assert.False(Minidump.IsValidMinidump(new StreamAddressSpace(stream)));

            using (FileStream stream = File.OpenRead(x64Dump))
                Assert.False(Minidump.IsValidMinidump(new StreamAddressSpace(stream)));
        }

        [Fact]
        public void CheckPdbInfo()
        {
            using (Stream stream = GetCrashDump(x86Dump))
                CheckPdbInfo(GetMinidumpFromStream(stream), x86ClrGuid);

            using (Stream stream = GetCrashDump(x64Dump))
                CheckPdbInfo(GetMinidumpFromStream(stream), x64ClrGuid);
        }

        private void CheckPdbInfo(Minidump minidump, Guid guid)
        {
            PEFile image = minidump.LoadedImages.Where(i => i.ModuleName.EndsWith(@"\clr.dll")).Single().Image;
            PEPdbRecord pdb = image.Pdb;

            Assert.NotNull(pdb);
            Assert.Equal(ClrPdb, pdb.Path);
            Assert.Equal(ClrAge, pdb.Age);
            Assert.Equal(guid, pdb.Signature);
        }

        [Fact]
        public void CheckModuleNames()
        {
            using (Stream stream = GetCrashDump(x86Dump))
                CheckModuleNames(GetMinidumpFromStream(stream));

            using (Stream stream = GetCrashDump(x64Dump))
                CheckModuleNames(GetMinidumpFromStream(stream));
        }

        private void CheckModuleNames(Minidump minidump)
        {
            Assert.Equal(1, minidump.LoadedImages.Where(i => i.ModuleName.EndsWith(@"\clr.dll")).Count());

            foreach (var module in minidump.LoadedImages)
                Assert.NotNull(module.ModuleName);
        }

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
                int count = memory.Where(m => m.VirtualAddress <= image.BaseAddress && image.BaseAddress < m.VirtualAddress + m.Size).Count();
                Assert.Equal(1, count);
                
                // Check the start of each image for the PE header 'MZ'
                byte[] header = minidump.VirtualAddressReader.Read(image.BaseAddress, 2);
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
