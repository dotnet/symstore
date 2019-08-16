// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.SymbolStore;
using Microsoft.SymbolStore.KeyGenerators;
using Microsoft.SymbolStore.SymbolStores;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SOS
{
    /// <summary>
    /// Basic http symbol store. The request can be authentication with a PAT for VSTS symbol stores.
    /// </summary>
    public class DirectorySymbolStore : SymbolStore
    {
        /// <summary>
        /// Directory to search symbols
        /// </summary>
        public string Directory { get; }

        /// <summary>
        /// Create an instance of a directory symbol store
        /// </summary>
        /// <param name="backingStore">next symbol store or null</param>
        /// <param name="directory">symbol search path</param>
        public DirectorySymbolStore(ITracer tracer, SymbolStore backingStore, string directory)
            : base(tracer, backingStore)
        {
            Directory = directory;
        }

        protected override Task<SymbolStoreFile> GetFileInner(SymbolStoreKey key, CancellationToken token)
        {
            SymbolStoreFile result = null;

            if (SymbolStoreKey.IsKeyValid(key.Index))
            {
                string filePath = Path.Combine(Directory, Path.GetFileName(key.FullPathName));
                if (File.Exists(filePath))
                {
                    try
                    {
                        Stream fileStream = File.OpenRead(filePath);
                        var file = new SymbolStoreFile(fileStream, filePath);
                        var generator = new FileKeyGenerator(Tracer, file);

                        foreach (SymbolStoreKey targetKey in generator.GetKeys(KeyTypeFlags.IdentityKey))
                        {
                            if (key.Equals(targetKey))
                            {
                                result = file;
                                break;
                            }
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                    {
                    }
                }
            }
            else
            {
                Tracer.Error("DirectorySymbolStore: invalid key index {0}", key.Index);
            }

            return Task.FromResult(result);
        }
    }
}
