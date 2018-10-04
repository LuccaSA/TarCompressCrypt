using CommandLine;
using TCC.Lib.Options;

namespace TCC.Parser
{
    [Verb("benchmark", HelpText = "Benchmark mode")]
    public class BenchmarkOptions
    {
        [Value(0, Required = false, MetaName = "Source", HelpText = "Test data file set for benchmark")]
        public string Source { get; set; }

        [Option('a', "algorithm", HelpText = "Algorithm : All (default), Lz4, Brotli, Zstd", Default = BenchmarkCompressionAlgo.All)]
        public BenchmarkCompressionAlgo Algorithm { get; set; }

        [Option('r', "ratio", HelpText = "Compression ratio. Valid values are : lz4 [1,9], brotli [1,11], zstd [1,19]. No value benchmarks all ratios ")]
        public int Ratio { get; set; }

        [Option('e', "encrypt", HelpText = "With or without encryption. Both tested by default", Default = null)]
        public bool? Encrypt { get; set; }

        [Option('c', "content", HelpText = "Ascii, Binary, Both. If no specified source, generated test file content. Both by default", Default = BenchmarkContent.Both)]
        public BenchmarkContent Content { get; set; }

        [Option('n', "number", HelpText = "Number of generated test files. Default = 1", Default = 1)]
        public int NumberOfFiles { get; set; }

        [Option('s', "size", HelpText = "Size of generated test files in Kb. Default = 102 400 (100Mb)", Default = 102400)]
        public int FileSize { get; set; }

        [Option('t', "threads", HelpText = "Number of parallel threads. Default = Processor logical core count", Default = 0)]
        public int Threads { get; set; }
    }


}