// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.FileFormats;

namespace TestHelpers
{
    public class MaxStreamReadHelper : IAddressSpace
    {
        private IAddressSpace _addressSpace;

        public ulong Max { get; private set; }

        public MaxStreamReadHelper(IAddressSpace address)
        {
            _addressSpace = address;
        }
        

        public ulong Length
        {
            get
            {
                return _addressSpace.Length;
            }
        }

        public uint Read(ulong position, byte[] buffer, uint bufferOffset, uint count)
        {
            ulong max = position + count;
            if (max > Max)
                Max = max;

            return _addressSpace.Read(position, buffer, bufferOffset, count);
        }
    }
}
