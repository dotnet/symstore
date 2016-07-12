// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FileFormats.ELF.Tests
{
    public class Tests
    {
        [Fact(Skip = "Need an alternate scheme to acquire the binary this test was reading")]
        public void CheckIndexingInfo()
        {
            using (FileStream libcoreclr = File.OpenRead("TestBinaries\\libcoreclr.so"))
            {
                StreamAddressSpace dataSource = new StreamAddressSpace(libcoreclr);
                ELFFile elf = new ELFFile(dataSource);
                string buildId = string.Concat(elf.BuildID.Select(b => b.ToString("x2")));

                //this is the build id for libcoreclr.so from package:
                // https://dotnet.myget.org/feed/dotnet-core/package/nuget/runtime.ubuntu.14.04-x64.Microsoft.NETCore.Runtime.CoreCLR/1.0.2
                Assert.Equal("bc0d85e535168f1a21a2dd79a466b3988bd274aa", buildId);
            }
        }

        [Fact(Skip = "Need an alternate scheme to acquire the binary this test was reading")]
        public void ParseCore()
        {
            using (FileStream core = File.OpenRead("TestBinaries\\core"))
            {
                StreamAddressSpace dataSource = new StreamAddressSpace(core);
                ELFCoreFile coreReader = new ELFCoreFile(dataSource);
                ELFLoadedImage loadedImage = coreReader.LoadedImages.Where(i => i.Path.EndsWith("libcoreclr.so")).First();
                string buildId = string.Concat(loadedImage.Image.BuildID.Select(b => b.ToString("x2")));

                //this is the build id for libcoreclr.so from package:
                // https://dotnet.myget.org/feed/dotnet-core/package/nuget/runtime.ubuntu.14.04-x64.Microsoft.NETCore.Runtime.CoreCLR/1.0.2
                Assert.Equal("bc0d85e535168f1a21a2dd79a466b3988bd274aa", buildId);
            }
        }
    }
}
