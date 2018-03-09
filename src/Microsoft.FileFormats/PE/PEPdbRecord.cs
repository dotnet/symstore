// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;

namespace Microsoft.FileFormats.PE
{
    public sealed class PEPdbRecord
    {
        public bool IsPortablePDB { get; private set; }
        public string Path { get; private set; }
        public Guid Signature { get; private set; }
        public int Age { get; private set; }

        public PEPdbRecord(bool isPortablePDB, string path, Guid sig, int age)
        {
            IsPortablePDB = isPortablePDB;
            Path = path;
            Signature = sig;
            Age = age;
        }
    }
}
