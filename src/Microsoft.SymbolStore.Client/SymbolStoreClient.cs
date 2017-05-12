// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;

namespace Microsoft.SymbolStore
{
    //  Consumer
    // 
    //  The consumer shall either use Windows-specific Protocol or Portable Protocol depending on its awareness of Windows PDB format and the version of the CodeView entry in Debug Directory of the PE file as follows: 
    // 
    //                      Windows PDB aware client (Visual Studio)  X-plat client (VS Code)
    // Version==0	        Windows-specific protocol                 Portable protocol  // built with Windows PDB (Portable PDB might also exist)
    // Version==0x0100504d	Portable protocol                         Portable protocol  // built with Portable PDB (no Windows PDB exists)
    // 
    // Windows-specific Protocol(backwards compatible)
    // Send request to the server for a value corresponding to key "{.pdb file name}/{PDB GUID}{Age}/{.pdb file name}".
    // The result is a Windows PDB file.
    // 
    // Portable Protocol
    // Send 2 requests to the server for values corresponding to keys "portable-pdb/{PDB ID}.pdb" and "portable-pdb/{PDB ID}.json". The request may be sent asynchronously.
    // The result of the former request is a Portable PDB file. The result of the latter request is a JSON file with format documented in section sources.json.

    public sealed class SymbolStoreClient
    {
        /// <summary>
        /// For example, https://dotnet.myget.org/F/dev-feed/symbols.
        /// </summary>
        public Uri StoreUri { get; }

        public SymbolStoreClient(Uri storeUri)
        {
            if (storeUri == null)
            {
                throw new ArgumentNullException(nameof(storeUri));
            }

            if (!storeUri.IsAbsoluteUri || !storeUri.IsFile && storeUri.Scheme != "http")
            {
                throw new ArgumentException(nameof(storeUri));
            }

            StoreUri = storeUri;
        }

        public async Task<Stream> GetSymbolFileForPortableExecutable(int version, Guid guid, int age, string fileName, bool portableOnly)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            // TODO: more arg validation

            // See https://github.com/dotnet/corefx/blob/master/src/System.Reflection.Metadata/specs/PE-COFF.md#codeview-debug-directory-entry-type-2 for specification of version
            ushort minorVersion = (ushort)version;
            ushort majorVersion = (ushort)(version >> 16);
            bool hasPortablePdb = minorVersion == 0x504d && majorVersion >= 0x100;
            if (hasPortablePdb && age != 1)
            {
                throw new BadImageFormatException();
            }

            string query;
            if (hasPortablePdb || portableOnly)
            {
                query = StoreQueryBuilder.GetPortablePdbQueryString(guid, fileName);
            }
            else
            {
                query = StoreQueryBuilder.GetWindowsPdbQueryString(guid, age, fileName);
            }

            // TODO: escape fileName?

            Uri requestUri;                                        
            if (!Uri.TryCreate(StoreUri, query, out requestUri))
            {
                throw new ArgumentException(nameof(fileName));
            }

            if (requestUri.IsFile)
            {
                // TODO: read file async
                return null;
            }
            else
            {
                using (var client = new HttpClient())
                {
                    // TODO: erorr handling
                    return await client.GetStreamAsync(requestUri).ConfigureAwait(false);
                }
            }
        }
    }
}
