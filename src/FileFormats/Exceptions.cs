// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileFormats
{
    /// <summary>
    /// Exception thrown to indicate that bits in the input cannot be parsed for whatever reason.
    /// </summary>
    public abstract class InputParsingException : Exception
    {
        public InputParsingException(String message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Exception thrown to indicate unparsable bits found in the input data being parsed.
    /// </summary>
    public class BadInputFormatException : InputParsingException
    {
        public BadInputFormatException(String message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Exception thrown to indicate errors during Layout construction. These errors are usually
    /// attributable to bugs in the parsing code, not errors in the input data.
    /// </summary>
    public class LayoutException : Exception
    {
        public LayoutException(String message)
            : base(message)
        {
        }
    }

}
