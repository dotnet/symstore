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
        private static string[] s_microsoftSymbolServerUrls = new string[] { "http://msdl.microsoft.com/download/symbols", "https://nuget.smbsrc.net", "http://referencesource.microsoft.com/symbols", "https://dotnet.myget.org/F/dotnet-core/symbols" };
        private static ISymbolServer[] s_microsoftSymbolServers = s_microsoftSymbolServerUrls.Select(url => new WindowsSymbolSever(url)).Cast<ISymbolServer>().ToArray();

        private ISymbolServer[] _symbolServers;

        public SymbolCache Cache { get; private set; }
        
        public static string DefaultSymbolCacheLocation
        {
            get
            {
                return Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "Symbols");
            }
        }

        public SymbolLocator()
            : this(new SymbolCache(DefaultSymbolCacheLocation), s_microsoftSymbolServers)
        {
        }

        public SymbolLocator(SymbolCache cache)
            : this(cache, s_microsoftSymbolServers)
        {
        }

        public SymbolLocator(SymbolCache cache, IEnumerable<ISymbolServer> symbolServers)
        {
            Cache = cache;
            _symbolServers = symbolServers.OrderBy(s=>!s.PreferThisServer).ToArray();
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
            SymbolServerResult result = await SearchServers((ISymbolServer server) => server.FindPdbAsync(pdbName, guid, age));
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
            SymbolServerResult result = await SearchServers((ISymbolServer server) => server.FindPEFileAsync(filename, timestamp, filesize));
            if (result == null)
                return null;

            // The result may be a cab file instead of the actual result.  In this case, decompress it.
            if (!result.Compressed)
                return Cache.StorePEFile(result.Stream, filename, timestamp, filesize);
            
            CabConverter converter = new CabConverter(result.Stream);
            using (MemoryStream stream = await converter.ConvertAsync())
                return Cache.StorePEFile(stream, filename, timestamp, filesize);
        }

        private async Task<SymbolServerResult> SearchServers(Func<ISymbolServer, Task<SymbolServerResult>> getTask)
        {
            IEnumerable<Task<SymbolServerResult>> tasks = _symbolServers.Where(server => server.PreferThisServer).Select(server => getTask(server));
            Task<SymbolServerResult> primaryResult = GetFirstNonNullResult(new List<Task<SymbolServerResult>>(tasks));

            tasks = _symbolServers.Where(server => !server.PreferThisServer).Select(server => getTask(server));
            Task<SymbolServerResult> secondaryResult = GetFirstNonNullResult(new List<Task<SymbolServerResult>>(tasks));

            return await primaryResult ?? await secondaryResult;
        }

        private async Task<SymbolServerResult> FindPdbWorkerAsync(string pdbName, Guid guid, int age)
        {
            List<Task<SymbolServerResult>> processing = new List<Task<SymbolServerResult>>(_symbolServers.Length);
            List<ISymbolServer> onDeck = new List<ISymbolServer>(_symbolServers.Length);

            Task<SymbolServerResult> preferredResult = SearchServers((ISymbolServer server) => server.PreferThisServer, (ISymbolServer server) => server.FindPdbAsync(pdbName, guid, age));
            Task<SymbolServerResult> secondaryResult = SearchServers((ISymbolServer server) => !server.PreferThisServer, (ISymbolServer server) => server.FindPdbAsync(pdbName, guid, age));

            SymbolServerResult result = await preferredResult;
            if (result != null)
                return result;

            return await secondaryResult;
        }

        private Task<SymbolServerResult> SearchServers(Func<ISymbolServer, bool> predicate, Func<ISymbolServer, Task<SymbolServerResult>> getTask)
        {
            var tasks = _symbolServers.Where(server => predicate(server)).Select(server => getTask(server));
            return GetFirstNonNullResult(new List<Task<SymbolServerResult>>(tasks));
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
    }
}
