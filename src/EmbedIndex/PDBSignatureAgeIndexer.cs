// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using FileFormats;
using FileFormats.PDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EmbedIndex
{
    public class PDBSignatureAgeIndexer : IFileFormatIndexer
    {
        public string ComputeIndexKey(string path, Stream fileStream)
        {
            try
            {
                if (Path.GetExtension(path) != ".pdb")
                {
                    return null;
                }
                PDBFile pdb = new PDBFile(new StreamAddressSpace(fileStream));
                if (!pdb.Header.IsMagicValid.Check())
                {
                    return null;
                }

                return pdb.Signature.ToString().Replace("-", "") + pdb.Age.ToString("X");
            }
            catch (InputParsingException)
            {
                return null;
            }
        }
    }
}
