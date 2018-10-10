// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SymbolStore.SymbolStores
{
    /// <summary>
    /// The symbol store for the internal symweb symbol server that handles the "file.ptr" support.
    /// </summary>
    public sealed class SymwebHttpSymbolStore : HttpSymbolStore
    {
        /// <summary>
        /// Create an instance of a http symbol store
        /// </summary>
        /// <param name="tracer">trace source for logging</param>
        /// <param name="backingStore">next symbol store or null</param>
        /// <param name="symbolServerUri">symbol server url</param>
        /// <param name="personalAccessToken">PAT or null if no authentication</param>
        public SymwebHttpSymbolStore(ITracer tracer, SymbolStore backingStore, Uri symbolServerUri, string personalAccessToken = null)
            : base(tracer, backingStore, symbolServerUri, personalAccessToken)
        {
        }

        protected override async Task<SymbolStoreFile> GetFileInner(SymbolStoreKey key, CancellationToken token)
        {
            SymbolStoreFile file = await base.GetFileInner(key, token);
            if (file != null)
            {
                return file;
            }
            Uri filePtrUri = GetRequestUri(key.IndexPrefix + "file.ptr");
            Stream filePtrStream = await GetFileStream(filePtrUri, token);
            if (filePtrStream != null)
            {
                using (filePtrStream)
                {
                    try
                    {
                        using (TextReader reader = new StreamReader(filePtrStream))
                        {
                            string filePtr = await reader.ReadToEndAsync();
                            Tracer.Verbose("SymwebHttpSymbolStore: file.ptr '{0}'", filePtr);
                            if (filePtr.StartsWith("PATH:"))
                            {
                                filePtr = filePtr.Replace("PATH:", "");
                                Stream stream = File.OpenRead(filePtr);
                                return new SymbolStoreFile(stream, filePtr);
                            }
                        }
                    }
                    catch (Exception ex) when (ex is InvalidOperationException || ex is IOException)
                    {
                        Tracer.Error("SymwebHttpSymbolStore: {0}", ex.Message);
                        MarkClientFailure();
                    }
                }
            }
            return null;
        }
    }
}
