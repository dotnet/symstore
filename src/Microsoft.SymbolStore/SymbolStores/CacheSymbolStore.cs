// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SymbolStore.SymbolStores
{
    public sealed class CacheSymbolStore : SymbolStore
    {
        public string CacheDirectory { get; }

        public CacheSymbolStore(ITracer tracer, SymbolStore backingStore, string cacheDirectory)
            : base(tracer, backingStore)
        {
            CacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
            Directory.CreateDirectory(cacheDirectory);
        }

        protected override Task<SymbolStoreFile> GetFileInner(SymbolStoreKey key, CancellationToken token)
        {
            SymbolStoreFile result = null;
            string cacheFile = GetCacheFilePath(key);
            if (File.Exists(cacheFile))
            {
                Stream fileStream = File.OpenRead(cacheFile);
                result = new SymbolStoreFile(fileStream, cacheFile);
            }
            return Task.FromResult(result);
        }

        protected override async Task WriteFileInner(SymbolStoreKey key, SymbolStoreFile file)
        {
            string cacheFile = GetCacheFilePath(key);
            if (cacheFile != null && !File.Exists(cacheFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cacheFile));
                using (Stream destinationStream = File.OpenWrite(cacheFile))
                {
                    await file.Stream.CopyToAsync(destinationStream);
                    Tracer.Verbose("Cached: {0}", cacheFile);
                }
            }
        }

        private string GetCacheFilePath(SymbolStoreKey key)
        {
            if (SymbolStoreKey.IsKeyValid(key.Index)) {
                return Path.Combine(CacheDirectory, key.Index);
            }
            Tracer.Error("CacheSymbolStore: invalid key index {0}", key.Index);
            return null;
        }
    }
}
