using CommandLine;
using TCC.Lib.Options;

namespace TCC.Parser
{
    [Verb("benchmark", HelpText = "Benchmark mode")]
    public class BenchmarkOptions
    {
        [Value(0, Required = true, MetaName = "Source", HelpText = "Test data file set for benchmark")]
        public string Source { get; set; }

        [Option('a', "algorithm", HelpText = "Algorithm : lz4 (default), brotli, zstd", Default = CompressionAlgo.Lz4)]
        public CompressionAlgo Algorithm { get; set; }

        [Option('r', "ratio", HelpText = "Compression ratio. Valid values are : lz4 [1,9], brotli [1,11], zstd [1,19] ")]
        public int Ratio { get; set; }

        [Option('e', "encrypt", HelpText = "With encryption", Default = false)]
        public bool Encrypt { get; set; }
    }
}