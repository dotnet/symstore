// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace Microsoft.SymbolStore
{
    internal static class StoreQueryBuilder
    {
        public static readonly string PdbPrefix = "";

        public static readonly string ElfImagePrefix = "elf-buildid-";
        public static readonly string ElfSymbolPrefix = "elf-buildid-sym-";

        public static readonly string MachImagePrefix = "mach-uuid-";
        public static readonly string MachSymbolPrefix = "mach-uuid-sym-";

        public static readonly string SourcePrefix = "sha1";

        public static string GetPortablePdbQueryString(Guid guid, string fileName)
        {
            return fileName + "/" + PdbPrefix + guid.ToString("N") + "ffffffff" + "/" + fileName;
        }

        public static string GetWindowsPdbQueryString(Guid guid, int age, string fileName)
        {
            return fileName + "/" + PdbPrefix + guid.ToString("N") + age.ToString() + "/" + fileName;
        }
    }
}
