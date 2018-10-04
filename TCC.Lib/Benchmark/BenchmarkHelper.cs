using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TCC.Lib.Blocks;
using TCC.Lib.Options;

namespace TCC.Lib.Benchmark
{
    public class BenchmarkHelper
    {
        private readonly TarCompressCrypt _tarCompressCrypt;
        public BenchmarkHelper(TarCompressCrypt tarCompressCrypt)
        {
            _tarCompressCrypt = tarCompressCrypt;
        }

        private IEnumerable<BenchmarIteration> GenerateBenchmarkIteration(BenchmarkOption benchmarkOption)
        {
            var algos = new[] {
                CompressionAlgo.Lz4,
                CompressionAlgo.Brotli,
                CompressionAlgo.Zstd
            };

            var withEncryption = new[] { false, true };

            foreach (CompressionAlgo algo in algos)
            {
                if (!ShouldTestAlgo(benchmarkOption, algo))
                {
                    continue;
                }

                foreach (bool encrypt in withEncryption)
                {
                    foreach (int ratio in GetBenchmarkRatios(benchmarkOption, algo))
                    {
                        yield return new BenchmarIteration
                        {
                            Algo = algo,
                            CompressionRatio = ratio,
                            Encryption = encrypt,
                        };
                    }
                }
            }
        }

        private static IEnumerable<int> GetBenchmarkRatios(BenchmarkOption benchmarkOption, CompressionAlgo algo)
        {
            if (benchmarkOption.Ratio == 0)
                return Enumerable.Range(1, MaxRatio(algo));
            else
                return new[] {benchmarkOption.Ratio};
        }

        private static bool ShouldTestAlgo(BenchmarkOption benchmarkOption, CompressionAlgo algo)
        {
            switch (algo)
            {
                case CompressionAlgo.Lz4:
                    return (benchmarkOption.Algorithm & BenchmarkCompressionAlgo.Lz4) == BenchmarkCompressionAlgo.Lz4;
                case CompressionAlgo.Brotli:
                    return (benchmarkOption.Algorithm & BenchmarkCompressionAlgo.Brotli) == BenchmarkCompressionAlgo.Brotli;
                case CompressionAlgo.Zstd:
                    return (benchmarkOption.Algorithm & BenchmarkCompressionAlgo.Zstd) == BenchmarkCompressionAlgo.Zstd;
                default:
                    throw new ArgumentOutOfRangeException(nameof(algo), algo, "Unknown algo");
            }
        }

        private static int MaxRatio(CompressionAlgo algo)
        {
            switch (algo)
            {
                case CompressionAlgo.Lz4:
                    return 9;
                case CompressionAlgo.Brotli:
                    return 9;
                case CompressionAlgo.Zstd:
                    return 19;
                default:
                    throw new ArgumentOutOfRangeException(nameof(algo));
            }
        }

        public async Task<OperationSummary> RunBenchmark(BenchmarkOption benchmarkOption)
        {
            var keysFolder = TestFileHelper.NewFolder();

            FileInfo src = new FileInfo(benchmarkOption.Source);

            var iterations = GenerateBenchmarkIteration(benchmarkOption).ToList();

            var operationSummaries = new List<OperationSummary>();

            foreach (var iter in iterations)
            {
                PasswordMode pm = iter.Encryption ? PasswordMode.PublicKey : PasswordMode.None;
                var compressedFolder = TestFileHelper.NewFolder();
                var outputFolder = TestFileHelper.NewFolder();

                // compress
                var compressOption = new CompressOption
                {
                    Algo = iter.Algo,
                    CompressionRatio = iter.CompressionRatio,
                    BlockMode = BlockMode.Explicit,
                    SourceDirOrFile = benchmarkOption.Source,
                    DestinationDir = compressedFolder,
                    Threads = Environment.ProcessorCount,
                    PasswordOption = await BenchmarkOptionHelper.GenerateCompressPassswordOption(pm, keysFolder)
                };

                Stopwatch swComp = Stopwatch.StartNew();

                var resultCompress = await _tarCompressCrypt.Compress(compressOption);
                swComp.Stop();

                operationSummaries.Add(resultCompress);
                foreach (var result in resultCompress.CommandResults)
                {
                    result.ThrowOnError();
                }

                var compressedFile = resultCompress.Blocks.Select(i => i.DestinationArchive).First();
                FileInfo fi = new FileInfo(compressedFile);

                double compressionFactor = src.Length / (double)fi.Length;

                // decompress
                var decompressOption = new DecompressOption
                {
                    SourceDirOrFile = compressedFile,
                    DestinationDir = outputFolder,
                    Threads = Environment.ProcessorCount,
                    PasswordOption = BenchmarkOptionHelper.GenerateDecompressPasswordOption(pm, keysFolder)
                };

                Stopwatch swDecomp = Stopwatch.StartNew();
                var resultDecompress = await _tarCompressCrypt.Decompress(decompressOption);
                swDecomp.Stop();

                operationSummaries.Add(resultDecompress);

                foreach (var result in resultDecompress.CommandResults)
                {
                    result.ThrowOnError();
                }

                double sizeMb = src.Length / (double)(1024 * 1024);
                double compMbs = sizeMb / (swComp.ElapsedMilliseconds / (float)1000);
                double decompMbs = sizeMb / (swDecomp.ElapsedMilliseconds / (float)1000);

                Console.Out.WriteLine($"{iter.Algo} [{iter.CompressionRatio}] aes={iter.Encryption} : compress {compMbs:0.###} Mb/s, decompress {decompMbs:0.###} Mb/s, ratio {compressionFactor:0.###}");
            }

            return new OperationSummary(operationSummaries.SelectMany(i => i.Blocks), operationSummaries.SelectMany(i => i.CommandResults));
        }
    }
}