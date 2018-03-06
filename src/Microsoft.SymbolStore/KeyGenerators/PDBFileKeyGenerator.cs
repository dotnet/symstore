// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.FileFormats;
using Microsoft.FileFormats.PDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.SymbolStore.KeyGenerators
{
    public class PDBFileKeyGenerator : KeyGenerator
    {
        private readonly PDBFile _pdbFile;
        private readonly string _path;

        public PDBFileKeyGenerator(ITracer tracer, SymbolStoreFile file)
            : base(tracer)
        {
            StreamAddressSpace dataSource = new StreamAddressSpace(file.Stream);
            _pdbFile = new PDBFile(dataSource);
            _path = file.FileName;
        }

        public override bool IsValid()
        {
            return _pdbFile.IsValid();
        }

        public override IEnumerable<SymbolStoreKey> GetKeys(KeyTypeFlags flags)
        {
            if (IsValid())
            {
                if ((flags & KeyTypeFlags.IdentityKey) != 0)
                {
                    yield return GetKey(_path, _pdbFile.Signature, unchecked((int)_pdbFile.Age));
                }
            }
        }

        /// <summary>
        /// Create a symbol store key for a Windows PDB.
        /// </summary>
        /// <param name="path">file name and path</param>
        /// <param name="signature">mvid guid</param>
        /// <param name="age">pdb age</param>
        /// <returns>symbol store key</returns>
        public static SymbolStoreKey GetKey(string path, Guid signature, int age)
        {
            Debug.Assert(path != null);
            Debug.Assert(signature != null);
            return BuildKey(path, string.Format("{0}{1:x}", signature.ToString("N"), age));
        }
    }
}