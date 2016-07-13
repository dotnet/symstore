// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace EmbedIndex
{
    public class SHA1Indexer : IFileFormatIndexer
    {
        public string ComputeIndexKey(string path, Stream fileStream)
        {
            if (!path.StartsWith("src/"))
            {
                return null;
            }

            byte[] hash = SHA1.Create().ComputeHash(fileStream);
            StringBuilder index = new StringBuilder();
            index.Append("sha1-");
            foreach (byte b in hash)
            {
                index.Append(b.ToString("x2"));
            }
            return index.ToString();
        }
    }
}
