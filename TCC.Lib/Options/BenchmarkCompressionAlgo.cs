using System;

namespace TCC.Lib.Options
{
    [Flags]
    public enum BenchmarkCompressionAlgo
    {
        Lz4 = 1 << 0,
        Brotli = 1 << 1,
        Zstd = 1 << 2,
        All = Lz4 | Brotli | Zstd
    }
}