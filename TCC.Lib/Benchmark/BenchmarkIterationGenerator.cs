using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TCC.Lib.Options;

namespace TCC.Lib.Benchmark
{
    public class BenchmarkIterationGenerator
    {
        public async Task<IEnumerable<BenchmarkIteration>> PrepareIteration(BenchmarkOption benchmarkOption)
        {
            var data = await PrepareTestData(benchmarkOption);

            return GenerateBenchmarkIteration(benchmarkOption, data).ToList();
        }

        public IEnumerable<BenchmarkIteration> GenerateBenchmarkIteration(BenchmarkOption benchmarkOption, IEnumerable<BenchmarkTestContent> testData)
        {
            var algos = new[] {
                CompressionAlgo.Lz4,
                CompressionAlgo.Brotli,
                CompressionAlgo.Zstd
            };

            var withEncryption = benchmarkOption.Encrypt == null ? new[] { false, true } : new[] { benchmarkOption.Encrypt.Value };

            foreach (var data in testData)
            {
                foreach (CompressionAlgo algo in algos.Where(a => ShouldTestAlgo(benchmarkOption, a)))
                {
                    foreach (bool encrypt in withEncryption)
                    {
                        foreach (int ratio in GetBenchmarkRatios(benchmarkOption, algo))
                        {
                            yield return new BenchmarkIteration
                            {
                                Algo = algo,
                                CompressionRatio = ratio,
                                Encryption = encrypt,
                                Content = data,
                            };
                        }
                    }
                }
            }
        }

        private async Task<List<BenchmarkTestContent>> PrepareTestData(BenchmarkOption benchmarkOption)
        {
            var list = new List<BenchmarkTestContent>();
            if (!String.IsNullOrEmpty(benchmarkOption.Source))
            {
                // explicit test file
                list.Add(new BenchmarkTestContent(benchmarkOption.Source, false, BenchmarkContent.UserDefined));
                return list;
            }

            // generate test data
            if ((benchmarkOption.Content & BenchmarkContent.Ascii) == BenchmarkContent.Ascii)
            {
                var testAsciiDataFolder = TestFileHelper.NewFolder();

                await Task.WhenAll(
                    Enumerable.Range(0, benchmarkOption.NumberOfFiles)
                        .Select(i => TestFileHelper.NewFile(testAsciiDataFolder, benchmarkOption.FileSize, true)));

                list.Add(new BenchmarkTestContent(testAsciiDataFolder, true, BenchmarkContent.Ascii));
            }

            if ((benchmarkOption.Content & BenchmarkContent.Binary) == BenchmarkContent.Binary)
            {
                var testBinaryDataFolder = TestFileHelper.NewFolder();

                await Task.WhenAll(
                    Enumerable.Range(0, benchmarkOption.NumberOfFiles)
                        .Select(i => TestFileHelper.NewFile(testBinaryDataFolder, benchmarkOption.FileSize)));

                list.Add(new BenchmarkTestContent(testBinaryDataFolder, true, BenchmarkContent.Binary));
            }
            return list;
        }

        private static IEnumerable<int> GetBenchmarkRatios(BenchmarkOption benchmarkOption, CompressionAlgo algo)
        {
            if (String.IsNullOrEmpty(benchmarkOption.Ratios))
                return Enumerable.Range(1, MaxRatio(algo));

            var ratios = UserDefinedRatios(benchmarkOption.Ratios, algo).ToList();
            return Enumerable.Range(ratios.Min(), ratios.Max() - ratios.Min() + 1);
        }

        private static IEnumerable<int> UserDefinedRatios(string ratios, CompressionAlgo algo)
        {
            foreach (var r in ratios.Split(','))
            {
                if (int.TryParse(r, out int ratio) && ratio > 0 && ratio <= MaxRatio(algo))
                {
                    yield return ratio;
                }
            }
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

    }
}