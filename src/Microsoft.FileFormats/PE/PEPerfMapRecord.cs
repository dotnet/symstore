// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;

namespace Microsoft.FileFormats.PE
{
    public sealed class PEPerfMapRecord
    {
        public string Path { get; private set; }
        public byte[] Signature { get; private set; }
        public uint Version { get; private set; }

        public PEPerfMapRecord(string path, byte[] sig, uint version)
        {
            Path = path;
            Signature = sig;
            Version = version;
        }
    }
}
