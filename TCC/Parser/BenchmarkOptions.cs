using System.Collections.Generic;
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

        [Option('r', "ratios", HelpText = "Compression ratios. Valid values are : lz4 [1,9], brotli [1,11], zstd [1,19] (ex : '3' or '4-7'). No value benchmarks all ratios !")]
        public string Ratios { get; set; }

        [Option('e', "encrypt", HelpText = "With or without encryption. Valid values are : 'true','false'. Both tested by default", Default = null)]
        public bool? Encrypt { get; set; }

        [Option('c', "content", HelpText = "Ascii, Binary, Both. Default = Both. (use only if explicit source parameter)", Default = BenchmarkContent.Both)]
        public BenchmarkContent Content { get; set; }

        [Option('n', "number", HelpText = "Number of generated test files. Default = 1. (use only if explicit source parameter)", Default = 1)]
        public int NumberOfFiles { get; set; }

        [Option('s', "size", HelpText = "Size of generated test files in Kb. Default = 102 400 (100Mb). (use only if explicit source parameter)", Default = 102400)]
        public int FileSize { get; set; }

        [Option('t', "threads", HelpText = "Number of parallel threads. Default = Processor logical core count", Default = 0)]
        public int Threads { get; set; }

        [Option('o', "outputCompressed", HelpText = "Output folder for compressed archives. Default = user temp folder", Default = null)]
        public string OutputCompressed { get; set; }

        [Option('d', "outputDecompressed", HelpText = "Output folder for archive decompression. Default = user temp folder", Default = null)]
        public string OutputDecompressed { get; set; }

        [Option('x', "cleanup", HelpText = "Cleanup compressed / decompressed data between each pass. Valid values are : 'true','false'", Default = false)]
        public bool Cleanup { get; set; }

        internal static readonly HashSet<string> AutoTestDataOptions = new HashSet<string>() { "-c", "--content", "-n", "--number", "-s", "--size" };
    }


}