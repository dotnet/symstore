// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.SymbolStore.Client
{
    internal static class StoreQueryBuilder
    {
        public static readonly string PortablePdbPrefix = "ppdb-sig-";
        public static readonly string WindowsPdbPrefix = "";

        public static readonly string ElfImagePrefix = "elf-buildid-";
        public static readonly string ElfSymbolPrefix = "elf-buildid-sym-";

        public static readonly string MachImagePrefix = "mach-uuid-";
        public static readonly string MachSymbolPrefix = "mach-uuid-sym-";

        public static readonly string SourcePrefix = "sha1";

        public static string GetPortablePdbQueryString(string filename, Guid guid, int age)
        {
            Debug.Assert(filename == Path.GetFileName(filename));  // parts of the path should have already been stripped
            Debug.Assert(age >= 0);

            string result = $"{filename}/{PortablePdbPrefix + guid.ToString("N") + age.ToString()}/{filename}";
            return result;
        }

        public static string GetWindowsPdbQueryString(string filename, Guid guid, int age)
        {
            Debug.Assert(filename == Path.GetFileName(filename));  // parts of the path should have already been stripped
            Debug.Assert(age >= 0);

            string result = $"{filename}/{WindowsPdbPrefix + guid.ToString("N") + age.ToString()}/{filename}";
            return result;
        }

        public static string GetPEFileIndexPath(string filename, int timestamp, int imagesize)
        {
            Debug.Assert(filename == Path.GetFileName(filename));  // parts of the path should have already been stripped
            string result = $"{filename}/{timestamp.ToString("x") + imagesize.ToString("x")}/{filename}";
            return result;
        }
    }
}
