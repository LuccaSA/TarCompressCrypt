using System;

namespace TCC.Lib.Options
{
    public enum CompressionAlgo
    {
        Lz4,
        Brotli,
        Zstd
    }

    [Flags]
    public enum BenchmarkCompressionAlgo
    {
        Lz4 = 1 << 1,
        Brotli = 1 << 2,
        Zstd = 1 << 3,
        All = Lz4 | Brotli | Zstd
    }
}