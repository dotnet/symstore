// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
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
            public PackageSymbolIndexEntry(string clientKey, FileReference cachedFileRef)
            {
                ClientKey = clientKey;
                CachedFileRef = cachedFileRef;
            }

            public string ClientKey { get; private set; }
            public FileReference CachedFileRef { get; private set; }

            public void Dispose()
            {
                CachedFileRef.Dispose();
                CachedFileRef = null;
            }
        }

        /// <summary>
        /// A mapping between each package and the list of symbol indexing entries it provides
        /// </summary>
        Dictionary<Package, List<PackageSymbolIndexEntry>> _packageSymbolIndex;

        /// <summary>
        /// A global mapping across all packages for clientKey => cached file
        /// </summary>
        Dictionary<string, List<FileReference>> _globalSymbolIndex;

        ILogger _logger;

        public PackageBasedSymbolStore(IFileStore cachedFileStorage, ILoggerFactory loggerFactory) : base(cachedFileStorage, loggerFactory)
        {
            _packageSymbolIndex = new Dictionary<Package, List<PackageSymbolIndexEntry>>();
            _globalSymbolIndex = new Dictionary<string, List<FileReference>>();
            _logger = loggerFactory.CreateLogger(GetType().FullName);
        }

        public FileReference GetSymbolFileRef(string clientKey)
        {
            List<FileReference> refs = new List<FileReference>();
            lock (this)
            {
                if (_globalSymbolIndex.TryGetValue(clientKey, out refs))
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
                    List<FileReference> refs;
                    if(!_globalSymbolIndex.TryGetValue(entry.ClientKey, out refs))
                    {
                        refs = new List<FileReference>();
                        _globalSymbolIndex.Add(entry.ClientKey, refs);
                    }
                    _logger.LogInformation("Adding symbol entry " + entry.ClientKey + " => " + entry.CachedFileRef.FilePath);
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
                    List<FileReference> refs = _globalSymbolIndex[entry.ClientKey];
                    refs.Remove(entry.CachedFileRef);
                    if (refs.Count == 0)
                    {
                        _globalSymbolIndex.Remove(entry.ClientKey);
                    }
                    entry.Dispose();
                }
            }
        }

        public class JsonSymbolIndexEntry
        {
            [JsonProperty(PropertyName="clientKey")]
            public string ClientKey;
            [JsonProperty(PropertyName="blobPath")]
            public string BlobPath;
        }

        List<PackageSymbolIndexEntry> ReadSymbolIndex(Package p, string symbolIndexJsonFilePath)
        {
            string jsonText = File.ReadAllText(symbolIndexJsonFilePath);
            List<JsonSymbolIndexEntry> jsonEntries = JsonConvert.DeserializeObject<List<JsonSymbolIndexEntry>>(jsonText);
            List<PackageSymbolIndexEntry> entries = new List<PackageSymbolIndexEntry>();
            foreach (JsonSymbolIndexEntry entry in jsonEntries)
            {
                FileReference cachedSymbolFile = p.GetFile(entry.BlobPath);
                if(cachedSymbolFile == null)
                {
                    //symbol packages shouldn't refer to files that don't exist in the package
                    throw new Exception("Badly formatted package");
                }
                entries.Add(new PackageSymbolIndexEntry(entry.ClientKey.ToLowerInvariant(), cachedSymbolFile));
            }
            return entries;
        }
    }
}
