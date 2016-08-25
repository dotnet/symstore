// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace Microsoft.SymbolStore
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

        public static string GetPortablePdbQueryString(Guid guid, uint stamp, string fileName)
        {
            return fileName + "/" + PortablePdbPrefix + guid.ToString("N") + stamp.ToString("x8") + "/" + fileName;
        }

        public static string GetWindowsPdbQueryString(Guid guid, int age, string fileName)
        {
            return fileName + "/" + WindowsPdbPrefix + guid.ToString("N") + age.ToString() + "/" + fileName;
        }
    }
}
