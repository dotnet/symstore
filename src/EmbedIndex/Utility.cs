using System;
using System.Collections.Generic;
using System.Text;

namespace EmbedIndex
{
    internal static class Utility
    {
        internal static string ToHexString(this byte[] bytes)
        {
            StringBuilder hex = new StringBuilder();
            foreach (byte b in bytes)
            {
                hex.Append(b.ToString("x2"));
            }
            return hex.ToString();
        }
    }
}
