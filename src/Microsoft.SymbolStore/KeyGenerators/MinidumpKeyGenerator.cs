// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.FileFormats;
using Microsoft.FileFormats.Minidump;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.SymbolStore.KeyGenerators
{
    public class MinidumpKeyGenerator : KeyGenerator
    {
        private readonly IAddressSpace _dataSource;

        public MinidumpKeyGenerator(ITracer tracer, SymbolStoreFile file)
            : base(tracer)
        {
            _dataSource = new StreamAddressSpace(file.Stream);
        }

        public override bool IsValid()
        {
            return Minidump.IsValid(_dataSource);
        }

        public override IEnumerable<SymbolStoreKey> GetKeys(KeyTypeFlags flags)
        {
            if (IsValid())
            {
                try
                {
                    var dump = new Minidump(_dataSource);
                    return dump.LoadedImages
                        .Select((MinidumpLoadedImage loadedImage) => new PEFileKeyGenerator(Tracer, loadedImage.Image, loadedImage.ModuleName))
                        .SelectMany((KeyGenerator generator) => generator.GetKeys(flags));
                }
                catch (InvalidVirtualAddressException ex)
                {
                    Tracer.Error("Minidump {0}", ex.Message);
                }
            }
            return SymbolStoreKey.EmptyArray;
        }
    }
}