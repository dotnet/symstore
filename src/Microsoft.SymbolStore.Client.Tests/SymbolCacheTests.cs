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
        readonly string CacheLocation = Path.Combine(Directory.GetCurrentDirectory(), @"test_output\cache");
        readonly byte[] FileData = new byte[] { 42, 0, 1, 2, 42 };

        [Fact]
        public void CheckPEFileSymbolCache()
        {
            ClearSymbolCache();

            SymbolCache cache = new SymbolCache(CacheLocation);
            
            // Ensure the file we are about to store doesn't exist.
            Assert.Null(cache.GetPEFileFromCache(PEFileName, PEFileTimestamp, PEFileSize));

            // Ensure removing the file doesn't throw an exception...this will return true because after the call the file is not in the cache
            Assert.True(cache.RemovePEFileFromCache(PEFileName, PEFileTimestamp, PEFileSize));

            string location = cache.StorePEFile(CreateMemoryStream(), PEFileName, PEFileTimestamp, PEFileSize);
            Assert.NotNull(location);
            Assert.True(File.Exists(location));

            Assert.True(location == cache.GetPEFileFromCache(PEFileName, PEFileTimestamp, PEFileSize));

            CheckFileMatches(location, FileData);

            Assert.True(cache.RemovePEFileFromCache(PEFileName, PEFileTimestamp, PEFileSize));
            Assert.False(File.Exists(location));

            Assert.True(Directory.Exists(CacheLocation));

            ClearSymbolCache();
        }

        private void ClearSymbolCache()
        {
            if (Directory.Exists(CacheLocation))
                Directory.Delete(CacheLocation, true);

            // Just make sure we are cleaned up.  This may fail if we leave a file locked, which is a bug.
            Assert.False(Directory.Exists(CacheLocation));
        }

        [Fact]
        public void CheckPdbFileSymbolCache()
        {
            if (Directory.Exists(CacheLocation))
                Directory.Delete(CacheLocation, true);
            Assert.False(Directory.Exists(CacheLocation));

            SymbolCache cache = new SymbolCache(CacheLocation);



            // Ensure the file we are about to store doesn't exist.
            Assert.Null(cache.GetPdbFromCache(PDBFileName, PDBGuid, PDBAge));

            // Ensure removing the file doesn't throw an exception...this will return true because after the call the file is not in the cache
            Assert.True(cache.RemovePdbFromCache(PDBFileName, PDBGuid, PDBAge));

            string location = cache.StorePdb(CreateMemoryStream(), PDBFileName, PDBGuid, PDBAge);
            Assert.NotNull(location);
            Assert.True(File.Exists(location));

            Assert.True(location == cache.GetPdbFromCache(PDBFileName, PDBGuid, PDBAge));

            CheckFileMatches(location, FileData);

            Assert.True(cache.RemovePdbFromCache(PDBFileName, PDBGuid, PDBAge));
            Assert.False(File.Exists(location));

            Assert.True(Directory.Exists(CacheLocation));
            Directory.Delete(CacheLocation, true);
            // Just make sure we are cleaned up.  This may fail if we leave a file locked, which is a bug.
            Assert.False(Directory.Exists(CacheLocation));
        }

        private void CheckFileMatches(string location, byte[] fileData)
        {

            using (FileStream fs = File.Open(location, FileMode.Open, FileAccess.Read))
            {
                Assert.True(fs.Length == FileData.Length);
                byte[] bytes = new byte[FileData.Length];
                Assert.True(fs.Read(bytes, 0, bytes.Length) == bytes.Length);

                for (int i = 0; i < bytes.Length; i++)
                    Assert.True(bytes[i] == FileData[i]);
            }
        }

        private MemoryStream CreateMemoryStream()
        {
            MemoryStream ms = new MemoryStream();
            ms.Write(FileData, 0, FileData.Length);
            ms.Position = 0;
            Assert.True(ms.Length == FileData.Length);
            return ms;
        }



    }
}
