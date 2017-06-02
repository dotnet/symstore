// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using FileFormats;
using FileFormats.PDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

                string filename = Path.GetFileName(path).ToLowerInvariant();
                StringBuilder key = new StringBuilder();
                key.Append(filename);
                key.Append("/");
                key.Append(pdb.Signature.ToString().Replace("-", "").ToLowerInvariant());
                key.Append(pdb.Age.ToString("x"));
                key.Append("/");
                key.Append(filename);
                return key.ToString();
            }
            catch (InputParsingException)
            {
                return null;
            }
        }
    }
}
