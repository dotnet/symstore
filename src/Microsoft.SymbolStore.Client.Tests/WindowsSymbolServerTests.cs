// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Xunit;

namespace Microsoft.SymbolStore.Client
{
    // Note that as an implementation detail, all async methods are equivalent in SymbolStore.Client,
    // so testing the non-async version of methods you end up hitting both code paths.
    public partial class Tests
    {
        const string PEFileName = "clr.dll";
        const int PEFileSize = 0x00965000;
        const int PEFileTimestamp = 0x4ba21eeb;

        const string PDBFileName = "clr.pdb";
        static Guid PDBGuid = new Guid("0a821b8a-573e-4289-9202-851df6a539f1");
        const int PDBAge = 2;

        [Fact]
        public void TestKnownPdbDownload()
        {
            // We should always be able to find 4.0 RTM on the public symbol server.
            WindowsSymbolSever server = CreateWindowsSymbolServer();
            SymbolServerResult result = server.FindPdb(PDBFileName, PDBGuid, PDBAge);
            
            Assert.NotNull(result);
            Assert.True(result.Compressed); // This particular symbol server should always provide us with compressed binaries
        }

        [Fact]
        public void TestKnownPEFileDownload()
        {
            // We should always be able to find 4.0 RTM on the public symbol server.
            WindowsSymbolSever server = CreateWindowsSymbolServer();
            Assert.True(server.IsRemoteServer);

            SymbolServerResult result = server.FindPEFile(PEFileName, PEFileTimestamp, PEFileSize);
            Assert.NotNull(result);
            Assert.True(result.Compressed); // This particular symbol server should always provide us with compressed binaries
        }

        private WindowsSymbolSever CreateWindowsSymbolServer()
        {
            return new WindowsSymbolSever("http://msdl.microsoft.com/download/symbols");
        }
    }
}
