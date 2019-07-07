using System;

namespace TCC.Lib.Options
{
    [Flags]
    public enum Mode
    {
        Unknown = 0,
        Compress = 1 << 0,
        Decompress = 1 << 1,
        Benchmark = Compress | Decompress
    }

}
