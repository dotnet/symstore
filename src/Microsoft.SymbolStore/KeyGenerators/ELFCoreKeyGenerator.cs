// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.FileFormats;
using Microsoft.FileFormats.ELF;
using Microsoft.FileFormats.PE;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.SymbolStore.KeyGenerators
{
    public class ELFCoreKeyGenerator : KeyGenerator
    {
        private readonly ELFCoreFile _core;

        public ELFCoreKeyGenerator(ITracer tracer, SymbolStoreFile file)
            : base(tracer)
        {
            StreamAddressSpace dataSource = new StreamAddressSpace(file.Stream);
            _core = new ELFCoreFile(dataSource);
        }

        public override bool IsValid()
        {
            return _core.IsValid();
        }

        public override IEnumerable<SymbolStoreKey> GetKeys(KeyTypeFlags flags)
        {
            if (IsValid())
            {
                return _core.LoadedImages
                    .Select((ELFLoadedImage loadedImage) => CreateGenerator(loadedImage))
                    .Where((KeyGenerator generator) => generator != null)
                    .SelectMany((KeyGenerator generator) => generator.GetKeys(flags));
            }
            return SymbolStoreKey.EmptyArray;
        }

        private KeyGenerator CreateGenerator(ELFLoadedImage loadedImage)
        {
            try
            {
                if (loadedImage.Image.IsValid())
                {
                    return new ELFFileKeyGenerator(Tracer, loadedImage.Image, loadedImage.Path);
                }
                // TODO - mikem 7/1/17 - need to figure out a better way to determine the file vs loaded layout
                bool layout = loadedImage.Path.StartsWith("/");
                var peFile = new PEFile(new RelativeAddressSpace(_core.DataSource, loadedImage.LoadAddress, _core.DataSource.Length), layout);
                if (peFile.IsValid())
                {
                    return new PEFileKeyGenerator(Tracer, peFile, loadedImage.Path);
                }
                Tracer.Warning("Unknown ELF core image {0:X16} {1}", loadedImage.LoadAddress, loadedImage.Path);
            }
            catch (InvalidVirtualAddressException ex)
            {
                Tracer.Error("{0}: {1:X16} {2}", ex.Message, loadedImage.LoadAddress, loadedImage.Path);
            }
            return null;
        }
    }
}