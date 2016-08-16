using FileFormats.PDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FileFormats;

namespace Microsoft.SymbolStore.Client
{
    public sealed class SymbolLocator
    {
        private const string c_privateSymbolServerName = "symweb.corp.microsoft.com";
        private static string[] s_microsoftSymbolServers = new string[] { "http://msdl.microsoft.com/download/symbols", "https://nuget.smbsrc.net", "http://referencesource.microsoft.com/symbols", "https://dotnet.myget.org/F/dotnet-core/symbols" };
        private static Task<WindowsSymbolSever> s_privateSymbolServer;
        private static bool s_usePrivateSymbolServer = true;
        private static ISymbolServer[] s_symbolServers;

        private ISymbolServer[] _symbolServers;

        public SymbolCache Cache { get; private set; }

        public ISymbolServer PrivateSymbolServer { get { return s_privateSymbolServer.Result; } }

        public static string DefaultSymbolCacheLocation
        {
            get
            {
                return Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "Symbols");
            }
        }

        public bool UsePrivateSymbolSever { get; set; }

        public SymbolLocator()
            : this(new SymbolCache(DefaultSymbolCacheLocation))
        {
        }

        public SymbolLocator(SymbolCache cache)
            : this(cache, s_symbolServers)
        {
        }

        public SymbolLocator(SymbolCache cache, IEnumerable<ISymbolServer> symbolServers)
        {
            Cache = cache;
            _symbolServers = symbolServers.ToArray();
        }
        
        public async Task<string> FindPdbAsync(string pdbName, Guid guid, int age)
        {
            string fullPath = pdbName;
            pdbName = Path.GetFileName(pdbName);

            // Does the PDB file exist on disk with the right criteria?  If so use it and don't archive
            // it in the cache.  (This scenario is for developers hitting F5 on their machine.)
            if (fullPath != pdbName && File.Exists(fullPath))
            {
                try
                {
                    using (FileStream fs = File.OpenRead(fullPath))
                    {
                        PDBFile pdb = new PDBFile(new StreamAddressSpace(fs));
                        if (pdb.Signature == guid && pdb.Age == age)
                            return fullPath;
                    }
                }
                catch
                {
                }
            }

            // Check the cache for the file.
            string cachePath = Cache.GetPdbFromCache(pdbName, guid, age);
            if (cachePath != null)
                return cachePath;

            // Otherwise attempt to find the file.
            SymbolServerResult result = await FindPdbWorkerAsync(pdbName, guid, age);
            if (result == null)
                return null;

            // The result may be a cab file instead of the actual result.  In this case, decompress it.
            if (!result.Compressed)
                return Cache.StorePdb(result.Stream, pdbName, guid, age);
            
            CabConverter converter = new CabConverter(result.Stream);
            using (MemoryStream stream = await converter.ConvertAsync())
                return Cache.StorePdb(stream, pdbName, guid, age);
        }

        public async Task<string> FindPEFileAsync(string filename, int timestamp, int filesize)
        {
            string fullPath = filename;
            filename = Path.GetFileName(fullPath);

            // Check the cache for the file.
            string cachePath = Cache.GetPEFileFromCache(filename, timestamp, filesize);
            if (cachePath != null)
                return cachePath;

            // Look for it on the server.
            SymbolServerResult result = await FindPEFileWorkerAsync(filename, timestamp, filesize);
            if (result == null)
                return null;

            // The result may be a cab file instead of the actual result.  In this case, decompress it.
            if (!result.Compressed)
                return Cache.StorePEFile(result.Stream, filename, timestamp, filesize);
            
            CabConverter converter = new CabConverter(result.Stream);
            using (MemoryStream stream = await converter.ConvertAsync())
                return Cache.StorePEFile(stream, filename, timestamp, filesize);
        }

        private async Task<SymbolServerResult> FindPEFileWorkerAsync(string filename, int timestamp, int filesize)
        {
            List<Task<SymbolServerResult>> processing = new List<Task<SymbolServerResult>>(_symbolServers.Length + 1);

            // PEFiles from either public or private symbol servers are the same, no need to treat them differently.
            // We just check all of them at once.
            if (s_usePrivateSymbolServer && UsePrivateSymbolSever)
            {
                ISymbolServer privateSymbolServer = await s_privateSymbolServer;
                if (privateSymbolServer != null)
                    processing.Add(privateSymbolServer.FindPEFileAsync(filename, timestamp, filesize));
            }

            processing.AddRange(_symbolServers.Select(server => server.FindPEFileAsync(filename, timestamp, filesize)));
            return await GetFirstNonNullResult(processing);
        }

        private async Task<SymbolServerResult> FindPdbWorkerAsync(string pdbName, Guid guid, int age)
        {

            // The internal symbol server can give private PDBs, so we actually check that location first and if it fails we fall
            // back to the public servers.
            SymbolServerResult result = null;
            if (s_usePrivateSymbolServer && UsePrivateSymbolSever)
            {
                ISymbolServer privateSymbolServer = await s_privateSymbolServer;
                if (privateSymbolServer != null)
                {
                    result = await privateSymbolServer.FindPdbAsync(pdbName, guid, age);
                    if (result != null)
                        return result;
                }
            }

            List<Task<SymbolServerResult>> processing = new List<Task<SymbolServerResult>>(_symbolServers.Select(server=>server.FindPdbAsync(pdbName, guid, age)));
            return await GetFirstNonNullResult(processing);
        }

        async Task<T> GetFirstNonNullResult<T>(List<Task<T>> tasks) where T : class
        {
            while (tasks.Count > 0)
            {
                Task<T> task = await Task.WhenAny(tasks);

                T result = task.Result;
                if (result != null)
                    return result;

                if (tasks.Count == 1)
                    break;

                tasks.Remove(task);
            }

            return null;
        }

        #region Static Constructor
        static SymbolLocator()
        {
            s_privateSymbolServer = new Task<WindowsSymbolSever>(FindPrivateSymbolServer);
            s_privateSymbolServer.Start();

            s_symbolServers = s_microsoftSymbolServers.Select(url => new WindowsSymbolSever(url)).Cast<ISymbolServer>().ToArray();
        }

        private static WindowsSymbolSever FindPrivateSymbolServer()
        {
            try
            {
                IPAddress[] result = null;
                Task<IPAddress[]> addresses = System.Net.Dns.GetHostAddressesAsync(c_privateSymbolServerName);
                
                if (addresses.Wait(700))
                    result = addresses.Result;

                if (result != null && result.Length > 0)
                    return new WindowsSymbolSever($"http://{c_privateSymbolServerName}");
            }
            catch
            {
            }

            s_usePrivateSymbolServer = false;
            return null;
        }
        #endregion
    }
}
