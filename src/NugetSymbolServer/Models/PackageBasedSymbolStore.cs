// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NugetSymbolServer.Service.Models
{
    public class PackageBasedSymbolStore : PackageStore, ISymbolAccess
    {
        class PackageSymbolIndexEntry : IDisposable
        {
            public PackageSymbolIndexEntry(string clientKey, string packageRelativeFilePath, FileReference cachedFileRef)
            {
                ClientKey = clientKey;
                PackageRelativeFilePath = packageRelativeFilePath;
                CachedFileRef = cachedFileRef;
            }

            public string ClientKey { get; private set; }
            public string PackageRelativeFilePath { get; private set; }
            public FileReference CachedFileRef { get; private set; }

            public void Dispose()
            {
                CachedFileRef.Dispose();
                CachedFileRef = null;
            }
        }

        public class SymbolStoreKey : Tuple<string, string>
        {
            public SymbolStoreKey(string clientKey, string filename) :
                base(clientKey.ToLowerInvariant(), filename.ToLowerInvariant())
            { }
        }

        /// <summary>
        /// A mapping between each package and the list of symbol indexing entries it provides
        /// </summary>
        Dictionary<Package, List<PackageSymbolIndexEntry>> _packageSymbolIndex;

        /// <summary>
        /// A global mapping across all packages for clientKey,filename => cached file
        /// </summary>
        Dictionary<SymbolStoreKey, List<FileReference>> _globalSymbolIndex;

        public PackageBasedSymbolStore(IFileStore cachedFileStorage) : base(cachedFileStorage)
        {
            _packageSymbolIndex = new Dictionary<Package, List<PackageSymbolIndexEntry>>();
            _globalSymbolIndex = new Dictionary<SymbolStoreKey, List<FileReference>>();
        }

        public FileReference GetSymbolFileRef(string clientKey, string fileName)
        {
            SymbolStoreKey key = new SymbolStoreKey(clientKey, fileName);
            List<FileReference> refs = new List<FileReference>();
            lock (this)
            {
                if (_globalSymbolIndex.TryGetValue(key, out refs))
                {
                    // arbitrarily select a result if there is more than one
                    return refs[0].Clone();
                }
            }
            return null;
            
        }

        protected override void OnPackageAdded(Package p)
        {
            FileReference symbolIndexFile = p.GetFile("symbol_index.json");
            if (symbolIndexFile == null)
            {
                return;
            }
            List<PackageSymbolIndexEntry> symbolIndex = null;
            using (symbolIndexFile)
            {
                symbolIndex = ReadSymbolIndex(p, symbolIndexFile.FilePath);
            }
            _packageSymbolIndex.Add(p, symbolIndex);
            lock(this)
            {
                foreach(PackageSymbolIndexEntry entry in symbolIndex)
                {
                    SymbolStoreKey key = new SymbolStoreKey(entry.ClientKey, Path.GetFileName(entry.PackageRelativeFilePath));
                    List<FileReference> refs;
                    if(!_globalSymbolIndex.TryGetValue(key, out refs))
                    {
                        refs = new List<FileReference>();
                        _globalSymbolIndex.Add(key, refs);
                    }
                    refs.Add(entry.CachedFileRef);
                }
            }
        }

        protected override void OnPackageRemoved(Package p)
        {
            List<PackageSymbolIndexEntry> symbolIndex;
            if (!_packageSymbolIndex.TryGetValue(p, out symbolIndex))
            {
                return;
            }
            _packageSymbolIndex.Remove(p);
            lock (this)
            {
                foreach (PackageSymbolIndexEntry entry in symbolIndex)
                {
                    SymbolStoreKey key = new SymbolStoreKey(entry.ClientKey, Path.GetFileName(entry.PackageRelativeFilePath));
                    List<FileReference> refs = _globalSymbolIndex[key];
                    refs.Remove(entry.CachedFileRef);
                    if (refs.Count == 0)
                    {
                        _globalSymbolIndex.Remove(key);
                    }
                    entry.Dispose();
                }
            }
        }

        List<PackageSymbolIndexEntry> ReadSymbolIndex(Package p, string symbolIndexJsonFilePath)
        {
            string jsonText = File.ReadAllText(symbolIndexJsonFilePath);
            Dictionary<string, string> jsonSymbolIndexEntries = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);
            List<PackageSymbolIndexEntry> entries = new List<PackageSymbolIndexEntry>();
            foreach (KeyValuePair<string,string> entry in jsonSymbolIndexEntries)
            {
                FileReference cachedSymbolFile = p.GetFile(entry.Value);
                if(cachedSymbolFile == null)
                {
                    //symbol packages shouldn't refer to files that don't exist in the package
                    throw new Exception("Badly formatted package");
                }
                entries.Add(new PackageSymbolIndexEntry(entry.Key, entry.Value, cachedSymbolFile));
            }
            return entries;
        }

        
    }
}
