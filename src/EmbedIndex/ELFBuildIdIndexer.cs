// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using FileFormats;
using FileFormats.ELF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EmbedIndex
{
    public class ELFBuildIdIndexer : IFileFormatIndexer
    {
        public string ComputeIndexKey(string path, Stream fileStream)
        {
            try
            {
                string extension = Path.GetExtension(path);
                if(!string.IsNullOrEmpty(extension) && extension != ".dbg")
                {
                    return null;
                }
                ELFFile elf = new ELFFile(new StreamAddressSpace(fileStream));
                if (!elf.Ident.IsIdentMagicValid.Check())
                {
                    return null;
                }
                //TODO: it would be nice to check if the file is really stripped rather than blindly
                //trusting the file extension
                bool isStripped = extension != ".dbg";
                return "elf-buildid-" + (isStripped ? "" : "sym-") + string.Concat(elf.BuildID.Select(b => b.ToString("x2")));
            }
            catch(InputParsingException)
            {
                return null;
            }
        }
    }
}
