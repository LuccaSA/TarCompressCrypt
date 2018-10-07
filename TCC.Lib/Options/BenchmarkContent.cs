using System;

namespace TCC.Lib.Options
{
    [Flags]
    public enum BenchmarkContent
    {
        None = 0,
        Ascii = 1 << 0,
        Binary = 1 << 1,
        UserDefined = 1 << 2,
        Both = Ascii | Binary
    }
}