// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileFormats
{
    /// <summary>
    /// An integral type that can be configured to parse as a 4 byte uint or 8 byte ulong
    /// </summary>
    public struct SizeT
    {
        ulong _value;
        internal SizeT(ulong value)
        {
            _value = value;
        }

        public static implicit operator ulong(SizeT instance)
        {
            return instance._value;
        }

        public static explicit operator long(SizeT instance)
        {
            return (long)instance._value;
        }

        public static explicit operator uint(SizeT instance)
        {
            return (uint)instance._value;
        }

        public override string ToString()
        {
            return "0x" + _value.ToString("x");
        }
    }

    public class UInt64SizeTLayout : LayoutBase
    {
        ILayout _storageLayout;
        public UInt64SizeTLayout(ILayout storageLayout) : base(typeof(SizeT), storageLayout.Size)
        {
            if(storageLayout.Type != typeof(ulong))
            {
                throw new ArgumentException("storageLayout must be for the System.UInt64 type");
            }
            _storageLayout = storageLayout;
        }

        public override object Read(IAddressSpace dataSource, ulong position)
        {
            return new SizeT((ulong)_storageLayout.Read(dataSource, position));
        }
    }

    public class UInt32SizeTLayout : LayoutBase
    {
        ILayout _storageLayout;
        public UInt32SizeTLayout(ILayout storageLayout) : base(typeof(SizeT), storageLayout.Size)
        {
            if (storageLayout.Type != typeof(uint))
            {
                throw new ArgumentException("storageLayout must be for the System.UInt32 type");
            }
            _storageLayout = storageLayout;
        }

        public override object Read(IAddressSpace dataSource, ulong position)
        {
            return new SizeT((uint)_storageLayout.Read(dataSource, position));
        }
    }

    public static partial class LayoutManagerExtensions
    {

        /// <summary>
        /// Adds support for parsing the SizeT type
        /// </summary>
        /// <param name="size">The number of bytes that should be parsed for SizeT, either 4 or 8</param>
        /// <remarks>
        /// SizeT reuses the existing parsing logic for either uint or ulong depending on size. The ILayoutManager
        /// is expected to already have the relevant type's layout defined before calling this method.
        /// </remarks>
        public static LayoutManager AddSizeT(this LayoutManager layouts, int size)
        {
            if(size == 4)
            {
                layouts.AddLayout(new UInt32SizeTLayout(layouts.GetLayout<uint>()));
            }
            else if(size == 8)
            {
                layouts.AddLayout(new UInt64SizeTLayout(layouts.GetLayout<ulong>()));
            }
            else
            {
                throw new ArgumentException("Size must be 4 or 8");
            }
            return layouts;
        }
    }
}
