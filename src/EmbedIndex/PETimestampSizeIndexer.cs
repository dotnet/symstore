// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using FileFormats;
using FileFormats.PE;
using System.Collections.Generic;
using System.IO;

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
                string key = reader.Timestamp.ToString("x8") + reader.SizeOfImage.ToString("x8");
                return key.ToLowerInvariant();
            }
            catch (InputParsingException)
            {
                return null;
            }
        }
    }
}
