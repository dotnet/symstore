// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileFormats
{
    /// <summary>
    /// A common base class to assist in implementing ILayout
    /// </summary>
    public abstract class LayoutBase : ILayout
    {
        public LayoutBase(Type type) : this(type, 0) { }
        public LayoutBase(Type type, uint size) : this(type, size, size) { }
        public LayoutBase(Type type, uint size, uint naturalAlignment) : this(type, size, naturalAlignment, size) { }
        public LayoutBase(Type type, uint size, uint naturalAlignment, uint sizeAsBaseType) : this(type, size, naturalAlignment, sizeAsBaseType, new IField[0]) { }
        public LayoutBase(Type type, uint size, uint naturalAlignment, uint sizeAsBaseType, IField[] fields)
        {
            Type = type;
            IsFixedSize = true;
            Size = size;
            SizeAsBaseType = sizeAsBaseType;
            NaturalAlignment = naturalAlignment;
            Fields = fields;
        }

        public IEnumerable<IField> Fields { get; private set; }

        public uint NaturalAlignment { get; private set; }

        public bool IsFixedSize { get; private set; }

        public uint Size { get; private set; }

        public uint SizeAsBaseType { get; private set; }

        public Type Type { get; private set; }

        public virtual object Read(IAddressSpace dataSource, ulong position, out uint bytesRead)
        {
            bytesRead = Size;
            return Read(dataSource, position);
        }

        public virtual object Read(IAddressSpace dataSource, ulong position)
        {
            throw new NotImplementedException();
        }
    }
}
