// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using FileFormats;
using FileFormats.MachO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmbedIndex
{
    public class MachOUuidIndexer : IFileFormatIndexer
    {
        public string ComputeIndexKey(string path, Stream fileStream)
        {
            try
            {
                string extension = Path.GetExtension(path);
                if (extension != ".dylib" && extension != ".dylib.dwarf")
                {
                    return null;
                }
                MachOFile machO = new MachOFile(new StreamAddressSpace(fileStream));
                if (!machO.HeaderMagic.IsMagicValid.Check())
                {
                    return null;
                }

                string filename = Path.GetFileName(path).ToLowerInvariant();
                StringBuilder key = new StringBuilder();
                key.Append(filename);
                key.Append("/mach-uuid-");
                //TODO: it would be nice to really check if the file is stripped rather than
                // assuming it is based on the extension
                bool isStripped = extension == ".dylib";
                key.Append(isStripped ? "" : "sym-");
                key.Append(string.Concat(machO.Uuid.Select(b => b.ToString("x2"))).ToLowerInvariant());
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
