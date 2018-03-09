// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.FileFormats
{
    public interface IField
    {
        string Name { get; }
        ILayout Layout { get; }
        ILayout DeclaringLayout { get; }
        uint Offset { get; }

        object GetValue(TStruct tStruct);
        void SetValue(TStruct tStruct, object fieldValue);
    }
}
