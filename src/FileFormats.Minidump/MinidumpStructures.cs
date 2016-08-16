using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileFormats.CrashDump
{
    public static class CrashDumpLayoutManagerExtensions
    {
        public static LayoutManager AddCrashDumpTypes(this LayoutManager layouts, bool isBigEndian, bool is64Bit)
        {
            return layouts
                     .AddPrimitives(isBigEndian)
                     .AddEnumTypes()
                     .AddSizeT(is64Bit ? 8 : 4)
                     .AddPointerTypes()
                     .AddNullTerminatedString()
                     .AddTStructTypes();
        }
    }
}
