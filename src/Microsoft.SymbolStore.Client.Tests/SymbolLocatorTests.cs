using FileFormats;
using FileFormats.PDB;
using FileFormats.PE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.SymbolStore.Client
{
    public partial class Tests
    {
        [Fact]
        public async void SymbolServerEndToEnd()
        {
            SymbolLocator locator = new SymbolLocator(new SymbolCache(CacheLocation), new ISymbolServer[] { CreateWindowsSymbolServer() });

            Task<string>[] tasks = new Task<string>[2];
            tasks[0] = locator.FindPEFileAsync(PEFileName, PEFileTimestamp, PEFileSize);
            tasks[1] = locator.FindPdbAsync(PDBFileName, PDBGuid, PDBAge);
            
            Task<string> result = await Task.WhenAny(tasks);

            if (result == tasks[0])
            {
                ValidatePEFile(locator, result.Result);
                ValidatePdb(locator, await tasks[1]);
            }
            else
            {
                ValidatePdb(locator, result.Result);
                ValidatePEFile(locator, await tasks[0]);
            }

            ClearSymbolCache();
        }

        private void ValidatePdb(SymbolLocator locator, string path)
        {
            Assert.NotNull(path);
            Assert.True(File.Exists(path));

            Assert.True(path == locator.Cache.GetPdbFromCache(PDBFileName, PDBGuid, PDBAge));

            using (FileStream fs = File.OpenRead(path))
            {
                PDBFile pdb = new PDBFile(new StreamAddressSpace(fs));
                Assert.True(pdb.Header.IsMagicValid.Check());
                Assert.True(pdb.Signature == PDBGuid);
                Assert.True(pdb.Age >= PDBAge);  // PDBs may have a later age than the one requested
            }
        }

        private void ValidatePEFile(SymbolLocator locator, string path)
        {
            Assert.NotNull(path);
            Assert.True(File.Exists(path));

            Assert.True(path == locator.Cache.GetPEFileFromCache(PEFileName, PEFileTimestamp, PEFileSize));

            using (FileStream fs = File.OpenRead(path))
            {
                PEFile pe = new PEFile(new StreamAddressSpace(fs));
                Assert.True(pe.HasValidPESignature.Check());
                Assert.True(pe.HasValidDosSignature.Check());

                Assert.True(pe.SizeOfImage == PEFileSize);
                Assert.True(pe.Timestamp == PEFileTimestamp);
            }
        }
    }
}
