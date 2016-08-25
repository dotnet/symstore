// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FileFormats.PE.Tests
{
    public class Tests
    {
        static readonly Guid Signature = new Guid("99891b3e-d7ae-4c3b-abff-8a2b4a9b0c43");
        static readonly int Age = 1;
        static readonly string Path = @"c:\users\noahfalk\documents\visual studio 2015\Projects\HelloWorld\HelloWorld\obj\Debug\HelloWorld.pdb";


        [Fact]
        public void CheckIndexingInfo()
        {
            using (Stream s = File.OpenRead("TestBinaries/HelloWorld.exe"))
            {
                StreamAddressSpace fileContent = new StreamAddressSpace(s);
                PEFile pe = new PEFile(fileContent);
                Assert.Equal((uint)0x8000, pe.SizeOfImage);
                Assert.Equal((uint)0x577F5919, pe.Timestamp);
            }
        }

        [Fact]
        public void CheckPdbInfo()
        {
            using (Stream s = File.OpenRead("TestBinaries/HelloWorld.exe"))
            {
                StreamAddressSpace fileContent = new StreamAddressSpace(s);
                PEFile pe = new PEFile(fileContent);
                PEPdbRecord pdb = pe.Pdb;
                Assert.Equal(Signature, pdb.Signature);
                Assert.Equal(Age, pdb.Age);
                Assert.Equal(Path, pdb.Path);
            }
        }
    }
}
