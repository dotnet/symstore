// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.SymbolStore.KeyGenerators;
using Microsoft.SymbolStore.SymbolStores;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.SymbolStore.Tests
{
    public class SymbolStoreTests
    {
        readonly ITracer _tracer;

        public SymbolStoreTests(ITestOutputHelper output)
        {
            _tracer = new Tracer(output);
        }

        [Fact]
        public async Task CacheSymbolStore()
        {
            using (Stream pdb = File.OpenRead("TestBinaries/HelloWorld.pdb")) {
                // Clean up any previous cache directories
                string cacheDirectory = "TestSymbolCache";
                try {
                    Directory.Delete(cacheDirectory, recursive: true);
                }
                catch (DirectoryNotFoundException) {
                }
                var inputFile = new SymbolStoreFile(pdb, "HelloWorld.pdb");
                var generator = new PDBFileKeyGenerator(_tracer, inputFile);

                IEnumerable<SymbolStoreKey> keys = generator.GetKeys(KeyTypeFlags.IdentityKey);
                Assert.True(keys.Count() == 1);
                SymbolStoreKey key = keys.First();

                var backingStore = new TestSymbolStore(_tracer, key, inputFile);
                var cacheSymbolStore = new CacheSymbolStore(_tracer, backingStore, cacheDirectory);

                // This should put HelloWorld.pdb into the cache
                SymbolStoreFile outputFile = await cacheSymbolStore.GetFile(key, CancellationToken.None);
                Assert.True(outputFile != null);

                // Should be the exact same instance given to TestSymbolStore
                Assert.True(inputFile == outputFile);

                // This should get it from the cache and not the backingStore
                backingStore.Dispose();
                outputFile = await cacheSymbolStore.GetFile(key, CancellationToken.None);
                Assert.True(outputFile != null);

                // Should NOT be the exact same SymbolStoreFile instance given to TestSymbolStore
                Assert.True(inputFile != outputFile);

                // Now make sure the output file from the cache is the same as the pdb we opened above
                CompareStreams(pdb, outputFile.Stream);
            }
        }

        private async Task DownloadFile(string path, bool ms, bool mi, string cache)
        {
            using (Stream stream = TestUtilities.OpenCompressedFile(path))
            {
                SymbolStoreFile file = new SymbolStoreFile(stream, path);
                SymbolStores.SymbolStore store = null;
                if (ms)
                {
                    Uri.TryCreate("http://msdl.microsoft.com/download/symbols/", UriKind.Absolute, out Uri uri);
                    store = new HttpSymbolStore(_tracer, store, uri);
                }
                if (mi)
                {
                    Uri.TryCreate("http://symweb.corp.microsoft.com/", UriKind.Absolute, out Uri uri);
                    store = new SymwebHttpSymbolStore(_tracer, store, uri);
                }
                if (cache != null)
                {
                    store = new CacheSymbolStore(_tracer, store, cache);
                }
                KeyTypeFlags flags = KeyTypeFlags.IdentityKey;
                var generator = new FileKeyGenerator(_tracer, file);

                IEnumerable<SymbolStoreKey> keys = generator.GetKeys(flags);
                foreach (SymbolStoreKey key in keys)
                {
                    using (SymbolStoreFile symbolFile = await store.GetFile(key, CancellationToken.None))
                    {
                        if (symbolFile != null)
                        {
                            CompareStreams(file.Stream, symbolFile.Stream);
                        }
                    }
                }
            }
        }

        private void CompareStreams(Stream stream1, Stream stream2)
        {
            Assert.True(stream1.Length == stream2.Length);

            stream1.Position = 0;
            stream2.Position = 0;

            for (int i = 0; i < stream1.Length; i++) {
                int b1 = stream1.ReadByte();
                int b2 = stream2.ReadByte();
                Assert.True(b1 == b2);
                if (b1 != b2) {
                    break;
                }
            }
        }

        sealed class TestSymbolStore : Microsoft.SymbolStore.SymbolStores.SymbolStore
        {
            readonly SymbolStoreKey _key;
            SymbolStoreFile _file;

            public TestSymbolStore(ITracer tracer, SymbolStoreKey key, SymbolStoreFile file)
                : base(tracer)
            {
                _key = key;
                _file = file;
            }

            protected override Task<SymbolStoreFile> GetFileInner(SymbolStoreKey key, CancellationToken token)
            {
                if (_file != null && key.Equals(_key))
                {
                    _file.Stream.Position = 0;
                    return Task.FromResult(_file);
                }
                return Task.FromResult<SymbolStoreFile>(null);
            }

            public override void Dispose()
            {
                _file = null;
                base.Dispose();
            }
        }
    }
}
