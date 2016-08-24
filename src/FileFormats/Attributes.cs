// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileFormats
{
    /// <summary>
    /// Attach to an array-typed targeted field to indicate the number of elements
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ArraySizeAttribute : Attribute
    {
        public ArraySizeAttribute(uint numElements)
        {
            NumElements = numElements;
        }

        public uint NumElements { get; private set; }
    }

    /// <summary>
    /// Attach to a field to indicate that it should be only be included in the type
    /// if a particular define has been enabled
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class IfAttribute : Attribute
    {
        public IfAttribute(string defineName)
        {
            DefineName = defineName;
        }

        public string DefineName { get; private set; }
    }
}
