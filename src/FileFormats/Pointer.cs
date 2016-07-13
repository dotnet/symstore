// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FileFormats
{
    /// <summary>
    /// A pointer layout that can create pointers from integral storage types
    /// </summary>
    public class PointerLayout : LayoutBase
    {
        protected ILayout _storageLayout;
        protected ILayout _targetLayout;

        public PointerLayout(Type pointerType, ILayout storageLayout, ILayout targetLayout) :
            base(pointerType, storageLayout.Size, storageLayout.NaturalAlignment)
        {
            _storageLayout = storageLayout;
            _targetLayout = targetLayout;
        }
    }

    /// <summary>
    /// A pointer layout that can create pointers from the System.UInt64 storage type 
    /// </summary>
    public class UInt64PointerLayout : PointerLayout
    {
        public UInt64PointerLayout(Type pointerType, ILayout storageLayout, ILayout targetLayout) :
            base(pointerType, storageLayout, targetLayout)
        {
            if (storageLayout.Type != typeof(ulong))
            {
                throw new ArgumentException("storageLayout must have System.UInt64 type");
            }
        }

        public override object Read(IAddressSpace dataSource, ulong position)
        {
            ulong val = (ulong)_storageLayout.Read(dataSource, position);
            Pointer p = (Pointer)Activator.CreateInstance(Type);
            p.Init(_targetLayout, val);
            return p;
        }
    }

    /// <summary>
    /// A pointer layout that can create pointers from the System.UInt32 storage type 
    /// </summary>
    public class UInt32PointerLayout : PointerLayout
    {
        public UInt32PointerLayout(Type pointerType, ILayout storageLayout, ILayout targetLayout) :
            base(pointerType, storageLayout, targetLayout)
        {
            if (storageLayout.Type != typeof(uint))
            {
                throw new ArgumentException("storageLayout must have System.UInt32 type");
            }
        }

        public override object Read(IAddressSpace dataSource, ulong position)
        {
            ulong val = (uint)_storageLayout.Read(dataSource, position);
            Pointer p = (Pointer)Activator.CreateInstance(Type);
            p.Init(_targetLayout, val);
            return p;
        }
    }

    /// <summary>
    /// A pointer layout that can create pointers from the SizeT storage type 
    /// </summary>
    public class SizeTPointerLayout : PointerLayout
    {
        public SizeTPointerLayout(Type pointerType, ILayout storageLayout, ILayout targetLayout) :
            base(pointerType, storageLayout, targetLayout)
        {
            if (storageLayout.Type != typeof(SizeT))
            {
                throw new ArgumentException("storageLayout must have SizeT type");
            }
        }

        public override object Read(IAddressSpace dataSource, ulong position)
        {
            ulong val = (SizeT)_storageLayout.Read(dataSource, position);
            Pointer p = (Pointer)Activator.CreateInstance(Type);
            p.Init(_targetLayout, val);
            return p;
        }
    }

    public class Pointer
    {
        public ulong Value;
        public bool IsNull
        {
            get { return Value == 0; }
        }

        public override string ToString()
        {
            return "0x" + Value.ToString("x");
        }

        public static implicit operator ulong (Pointer instance)
        {
            return instance.Value;
        }

        internal void Init(ILayout targetLayout, ulong value)
        {
            _targetLayout = targetLayout;
            Value = value;
        }

        protected ILayout _targetLayout;
    }

    /// <summary>
    /// A pointer that can be dereferenced to produce another object
    /// </summary>
    /// <typeparam name="TargetType">The type of object that is produced by dereferencing the pointer</typeparam>
    /// <typeparam name="StorageType">The type that determines how the pointer's underlying address value is parsed</typeparam>
    public class Pointer<TargetType, StorageType> : Pointer
    {
        /// <summary>
        /// Read an object of _TargetType_ from the _addressSpace_
        /// </summary>
        public TargetType Dereference(IAddressSpace addressSpace)
        {
            return Element(addressSpace, 0);
        }

        /// <summary>
        /// Read the array element _index_ from an array in _addressSpace_
        /// </summary>
        public TargetType Element(IAddressSpace addressSpace, uint index)
        {
            if (Value != 0)
                return (TargetType)_targetLayout.Read(addressSpace, Value + index * _targetLayout.Size);
            else
                return default(TargetType);
        }
    }

    public static partial class LayoutManagerExtensions
    {
        /// <summary>
        /// Adds support for reading types derived from Pointer<,>
        /// </summary>
        public static LayoutManager AddPointerTypes(this LayoutManager layouts)
        {
            layouts.AddLayoutProvider(GetPointerLayout);
            return layouts;
        }

        private static ILayout GetPointerLayout(Type pointerType, LayoutManager layoutManager)
        {
            if (!typeof(Pointer).GetTypeInfo().IsAssignableFrom(pointerType))
            {
                return null;
            }
            Type curPointerType = pointerType;
            TypeInfo genericPointerTypeInfo = null;
            while (curPointerType != typeof(Pointer))
            {
                TypeInfo curPointerTypeInfo = curPointerType.GetTypeInfo();
                if (curPointerTypeInfo.IsGenericType && curPointerTypeInfo.GetGenericTypeDefinition() == typeof(Pointer<,>))
                {
                    genericPointerTypeInfo = curPointerTypeInfo;
                    break;
                }
                curPointerType = curPointerTypeInfo.BaseType;
            }
            if (genericPointerTypeInfo == null)
            {
                throw new LayoutException("Pointer types must be derived from Pointer<,,>");
            }
            Type targetType = genericPointerTypeInfo.GetGenericArguments()[0];
            Type storageType = genericPointerTypeInfo.GetGenericArguments()[1];
            ILayout targetLayout = layoutManager.GetLayout(targetType);
            ILayout storageLayout = layoutManager.GetLayout(storageType);

            // Unforetunately the storageLayout.Read returns a boxed object that can't be 
            // casted to a ulong without first being unboxed. These three Pointer layout 
            // types are identical other than unboxing to a different type. Generics 
            // doesn't work, there is no constraint that ensures the type parameter defines
            // a casting operator to ulong. Specifying a Func<object,ulong> parameter
            // would work, but I opted to write each class seperately so that we don't
            // pay the cost of an extra delegate invocation for each pointer read. It
            // may be premature optimization, but the complexity of it should be relatively
            // constrained within this file at least.

            if (storageLayout.Type == typeof(SizeT))
            {
                return new SizeTPointerLayout(pointerType, storageLayout, targetLayout);
            }
            else if (storageLayout.Type == typeof(ulong))
            {
                return new UInt64PointerLayout(pointerType, storageLayout, targetLayout);
            }
            else if (storageLayout.Type == typeof(uint))
            {
                return new UInt32PointerLayout(pointerType, storageLayout, targetLayout);
            }
            else
            {
                throw new LayoutException("Pointer types must have a storage type of SizeT, ulong, or uint");
            }
        }
    }
}
