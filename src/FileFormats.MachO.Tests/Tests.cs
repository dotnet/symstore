// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FileFormats.MachO.Tests
{
    public class Tests
    {
        [Fact(Skip = "Need an alternate scheme to acquire the binary this test was reading")]
        public void CheckIndexingInfo()
        {
            //https://dotnet.myget.org/feed/dotnet-core/package/nuget/runtime.osx.10.10-x64.Microsoft.NETCore.Runtime.CoreCLR/1.0.2
            using (FileStream dylib = File.OpenRead("TestBinaries\\libcoreclr.dylib"))
            {
                StreamAddressSpace dataSource = new StreamAddressSpace(dylib);
                MachOFile machO = new MachOFile(dataSource);
                Assert.Equal(Guid.Parse("c988806d-a15d-5e3d-9a26-42cedad97a2f"), new Guid(machO.Uuid));
            }
        }

        [Fact(Skip = "Need an alternate scheme to acquire the binary this test was reading")]
        public void CheckDwarfIndexingInfo()
        {
            //https://dotnet.myget.org/feed/dotnet-core/package/nuget/runtime.osx.10.10-x64.Microsoft.NETCore.Runtime.CoreCLR/1.0.2
            using (FileStream dwarf = File.OpenRead("TestBinaries\\libcoreclr.dylib.dwarf"))
            {
                StreamAddressSpace dataSource = new StreamAddressSpace(dwarf);
                MachOFile machO = new MachOFile(dataSource);
                Assert.Equal(Guid.Parse("c988806d-a15d-5e3d-9a26-42cedad97a2f"), new Guid(machO.Uuid));
            }
        }

        [Fact(Skip = "Need an alternate scheme to acquire the binary this test was reading")]
        public void ParseCore()
        {
            using (FileStream core = File.OpenRead("TestBinaries\\core.12985"))
            {
                StreamAddressSpace dataSource = new StreamAddressSpace(core);
                // hard-coding the dylinker position so we don't pay to search for it each time
                // the code is capable of finding it by brute force search even if we don't provide the hint
                MachCore coreReader = new MachCore(dataSource, 0x00007fff68a59000);
                MachLoadedImage[] images = coreReader.LoadedImages.Where(i => i.Path.EndsWith("libcoreclr.dylib")).ToArray();
                MachOFile libCoreclr = images[0].Image;
                Assert.Equal(Guid.Parse("c988806d-a15d-5e3d-9a26-42cedad97a2f"), new Guid(libCoreclr.Uuid));
            }
        }
    }
}
