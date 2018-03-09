// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.FileFormats
{
    internal class ArrayLayout : LayoutBase
    {
        public ArrayLayout(Type arrayType, ILayout elementLayout, uint numElements) :
            base(arrayType, numElements * elementLayout.Size, elementLayout.NaturalAlignment)
        {
            _elementLayout = elementLayout;
            _numElements = numElements;
        }

        public override object Read(IAddressSpace dataSource, ulong position)
        {
            ulong src = position;
            uint elementSize = _elementLayout.Size;
            Array a = Array.CreateInstance(_elementLayout.Type, (int)_numElements);
            for (uint i = 0; i < _numElements; i++)
            {
                a.SetValue(_elementLayout.Read(dataSource, src), (int)i);
                src += elementSize;
            }
            return a;
        }

        private uint _numElements;
        private ILayout _elementLayout;
    }
}
