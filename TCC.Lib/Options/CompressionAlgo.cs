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
        Lz4 = 1 << 0,
        Brotli = 1 << 1,
        Zstd = 1 << 2,
        All = Lz4 | Brotli | Zstd
    }

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