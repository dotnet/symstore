// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.FileFormats;
using Microsoft.FileFormats.MachO;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.SymbolStore.KeyGenerators
{
    public class MachOFatHeaderKeyGenerator : KeyGenerator
    {
        private readonly MachOFatFile _machoFatFile;
        private readonly string _path;

        public MachOFatHeaderKeyGenerator(ITracer tracer, SymbolStoreFile file)
            : base(tracer)
        {
            _machoFatFile = new MachOFatFile(new StreamAddressSpace(file.Stream));
            _path = file.FileName;
        }

        public override bool IsValid()
        {
            return _machoFatFile.IsValid();
        }

        public override IEnumerable<SymbolStoreKey> GetKeys(KeyTypeFlags flags)
        {
            if (IsValid())
            {
                return _machoFatFile.ArchSpecificFiles.Select((file) => new MachOFileKeyGenerator(Tracer, file, _path)).SelectMany((generator) => generator.GetKeys(flags));
            }
            return SymbolStoreKey.EmptyArray;
        }
    }
}
