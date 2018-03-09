// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace Microsoft.SymbolStore
{
    /// <summary>
    /// A simple trace/logging interface.
    /// </summary>
    public interface ITracer
    {
        void WriteLine(string message);

        void WriteLine(string format, params object[] arguments);

        void Information(string message);

        void Information(string format, params object[] arguments);

        void Warning(string message);

        void Warning(string format, params object[] arguments);

        void Error(string message);

        void Error(string format, params object[] arguments);

        void Verbose(string message);

        void Verbose(string format, params object[] arguments);
    }
}
