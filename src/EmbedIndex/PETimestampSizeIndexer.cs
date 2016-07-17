// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using FileFormats;
using FileFormats.PE;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EmbedIndex
{
    public class PETimestampSizeIndexer : IFileFormatIndexer
    {
        public string ComputeIndexKey(string path, Stream fileStream)
        {
            string extension = Path.GetExtension(path);
            if (extension != ".dll" && extension != ".exe")
            {
                return null;
            }

            StreamAddressSpace fileAccess = new StreamAddressSpace(fileStream);
            try
            {
                PEFile reader = new PEFile(fileAccess);
                if (!reader.HasValidDosSignature.Check())
                {
                    return null;
                }
                string filename = Path.GetFileName(path).ToLowerInvariant();
                StringBuilder key = new StringBuilder();
                key.Append(filename);
                key.Append("/");
                key.Append(reader.Timestamp.ToString("x").ToLowerInvariant());
                key.Append(reader.SizeOfImage.ToString("x").ToLowerInvariant());
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
