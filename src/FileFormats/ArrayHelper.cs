// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileFormats
{
    public static class ArrayHelper
    {
        /// <summary>
        /// Safe array allocator - turns OverFlows and OutOfMemory into BIF's.
        /// </summary>
        public static E[] New<E>(uint count)
        {
            E[] a;
            try
            {
                a = new E[count];
            }
            catch (Exception)
            {
                throw new BadInputFormatException("Internal overflow attempting to allocate an array of size " + count + ".");
            }
            return a;
        }
    }
}
