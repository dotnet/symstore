using FileFormats.CrashDump;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestHelpers;
using Xunit;

namespace FileFormats.Minidumps
{
    public class Tests
    {
        [Fact(Skip ="Need data file")]
        public void CheckLoadedModules()
        {
            using (Stream stream = File.OpenRead("TestBinaries/crash_x86.dmp"))
            {
                StreamAddressSpace sas = new StreamAddressSpace(stream);
                Minidump minidump = new Minidump(sas);

                var modules = minidump.LoadedImages;
                Assert.True(modules.Length > 0);
            }
        }

        [Fact(Skip = "Need data file")]
        public void CheckStartupMemoryRead()
        {
            using (Stream stream = File.OpenRead("TestBinaries/crash_x86.dmp"))
            {
                StreamAddressSpace sas = new StreamAddressSpace(stream);
                MaxStreamReadHelper readHelper = new MaxStreamReadHelper(sas);

                Minidump minidump = new Minidump(readHelper);

                // We should have read the header of a minidump, so we cannot have read nothing.
                Assert.True(readHelper.Max > 0);

                // We should only read the header and not too far into the dump file, 1k should be plenty.
                Assert.True(readHelper.Max <= 1024);
            }
        }
    }
}
