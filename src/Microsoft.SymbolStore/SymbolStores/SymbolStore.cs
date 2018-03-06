// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SymbolStore.SymbolStores
{
    public abstract class SymbolStore : IDisposable
    {
        /// <summary>
        /// Next symbol store to chain if this store refuses the request
        /// </summary>
        private readonly SymbolStore _backingStore;

        /// <summary>
        /// Trace/logging source
        /// </summary>
        protected readonly ITracer Tracer;

        public SymbolStore(ITracer tracer)
        {
            Tracer = tracer;
        }

        public SymbolStore(ITracer tracer, SymbolStore backingStore)
            : this(tracer)
        {
            _backingStore = backingStore;
        }

        /// <summary>
        /// Downloads the file or retrieves it from a cache from the symbol store chain.
        /// </summary>
        /// <param name="key">symbol index to retrieve</param>
        /// <param name="token">to cancel requests</param>
        /// <returns>file or null if not found</returns>
        public async Task<SymbolStoreFile> GetFile(SymbolStoreKey key, CancellationToken token)
        {
            SymbolStoreFile file = await GetFileInner(key, token);
            if (file == null)
            {
                if (_backingStore != null)
                {
                    file = await _backingStore.GetFile(key, token);
                    if (file != null)
                    {
                        await WriteFileInner(key, file);

                        // Reset stream to the beginning for next symbol store
                        file.Stream.Position = 0;
                    }
                }
            }
            return file;
        }

        protected virtual Task<SymbolStoreFile> GetFileInner(SymbolStoreKey key, CancellationToken token)
        {
            return Task.FromResult<SymbolStoreFile>(null);
        }

        protected virtual Task WriteFileInner(SymbolStoreKey key, SymbolStoreFile file)
        {
            return Task.FromResult(0);
        }

        public virtual void Dispose()
        {
            if (_backingStore != null)
            {
                _backingStore.Dispose();
            }
        }
    }
}
