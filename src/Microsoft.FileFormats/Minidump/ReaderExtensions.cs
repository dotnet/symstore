// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Text;

namespace Microsoft.FileFormats.Minidump
{
    internal static class MinidumpReaderExtensions
    {
        public static string ReadCountedString(this Reader self, ulong position, Encoding encoding)
        {
            uint elementCount = self.Read<uint>(ref position);
            byte[] buffer = self.Read(position, elementCount);
            return encoding.GetString(buffer);
        }

        public static T[] ReadCountedArray<T>(this Reader self, ulong position)
        {
            uint elementCount = self.Read<uint>(ref position);
            var layout = self.LayoutManager.GetArrayLayout<T[]>(elementCount);
            return (T[])self.LayoutManager.GetArrayLayout<T[]>(elementCount).Read(self.DataSource, position);
        }
    }
}
