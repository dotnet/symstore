// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FileFormats.PDB.Tests
{
    public class Tests
    {
        [Fact(Skip ="pdb is missing")]
        public void CheckIndexingInfo()
        {
            using (Stream s = File.OpenRead("TestBinaries/HelloWorld.pdb"))
            {
                StreamAddressSpace fileContent = new StreamAddressSpace(s);
                PDBFile pdb = new PDBFile(fileContent);
                Assert.True(pdb.Header.IsMagicValid.Check());
                Assert.Equal((uint)1, pdb.Age);
                Assert.Equal(Guid.Parse("99891B3E-D7AE-4C3B-ABFF-8A2B4A9B0C43"), pdb.Signature);
            }
        }
    }
}
