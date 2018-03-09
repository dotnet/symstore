// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Xunit.Abstractions;

namespace Microsoft.SymbolStore.Tests
{
    /// <summary>
    /// Simple trace/logging support.
    /// </summary>
    internal sealed class Tracer : ITracer
    {
        private readonly ITestOutputHelper _output;

        public Tracer(ITestOutputHelper output)
        {
            _output = output;
        }

        public void WriteLine(string message)
        {
            _output.WriteLine(message);
        }

        public void WriteLine(string format, params object[] arguments)
        {
            _output.WriteLine(format, arguments);
        }

        public void Information(string message)
        {
            _output.WriteLine(message);
        }

        public void Information(string format, params object[] arguments)
        {
            _output.WriteLine(format, arguments);
        }

        public void Warning(string message)
        {
            _output.WriteLine("WARNING: " + message);
        }

        public void Warning(string format, params object[] arguments)
        {
            _output.WriteLine("WARNING: " + format, arguments);
        }

        public void Error(string message)
        {
            _output.WriteLine("ERROR: " + message);
        }

        public void Error(string format, params object[] arguments)
        {
            _output.WriteLine("ERROR: " + format, arguments);
        }

        public void Verbose(string message)
        {
        }

        public void Verbose(string format, params object[] arguments)
        {
        }
    }
}
