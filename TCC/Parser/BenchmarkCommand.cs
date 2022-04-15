using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;
using TCC.Lib.Options;

namespace TCC.Parser
{
    public class BenchmarkCommand : TccCommand<BenchmarkOption>
    {
        public BenchmarkCommand()
            : base("benchmark", "Benchmark mode")
        {
        }

        protected override IEnumerable<Argument> CreateArguments()
        {
            yield return new Argument<IEnumerable<string>> { Arity = ArgumentArity.OneOrMore, Name = "source", Description = "Test data file set for benchmark" };
        }

        protected override IEnumerable<Option> CreateOptions()
        {
            yield return new Option<BenchmarkCompressionAlgo>(new[] { "-a", "algorithm" }, description: "Algorithm : All (default), Lz4, Brotli, Zstd", getDefaultValue: () => BenchmarkCompressionAlgo.All);
            yield return new Option<string>(new[] { "-r", "--ratios" }, "Compression ratios. Valid values are : lz4 [1,9], brotli [1,11], zstd [1,19] (ex : '3' or '4-7'). No value benchmarks all ratios !");
            yield return new Option<bool?>(new[] { "-e", "--encrypt" }, "With or without encryption. Valid values are : 'true','false'. Both tested by default");
            yield return new Option<BenchmarkContent>(new[] { "-c", "--content" }, description: "Ascii, Binary, Both. Default = Both. (use only if explicit source parameter)", getDefaultValue: () => BenchmarkContent.Both);
            yield return new Option<int>(new[] { "-n", "--number" }, description: "Number of generated test files. Default = 1. (use only if explicit source parameter)", getDefaultValue: () => 1);
            yield return new Option<int>(new[] { "-s", "--size" }, description: "Size of generated test files in Kb. Default = 102 400 (100Mb). (use only if explicit source parameter)", getDefaultValue: () => 102400);
            yield return new Option<int>(new[] { "-t", "--threads" }, "Number of parallel threads. Default = Processor logical core count");
            yield return new Option<string>(new[] { "-o", "--outputCompressed" }, "Output folder for compressed archives. Default = user temp folder");
            yield return new Option<string>(new[] { "-o", "--outputDecompressed" }, "Output folder for archive decompression. Default = user temp folder");
            yield return new Option<bool>(new[] { "-x", "--cleanup" }, "Cleanup compressed / decompressed data between each pass. Valid values are : 'true','false'");
        }

        protected override Task RunAsync(ITccController controller, BenchmarkOption option) => controller.BenchmarkAsync(option);

        internal static readonly HashSet<string> AutoTestDataOptions = new HashSet<string>
            { "-c", "--content", "-n", "--number", "-s", "--size" };
    }
}