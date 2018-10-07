using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Blocks;
using TCC.Lib.Command;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Benchmark
{
    public class BenchmarkHelper
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly TarCompressCrypt _tarCompressCrypt;
        private readonly BenchmarkOptionHelper _benchmarkOptionHelper;

        public BenchmarkHelper(TarCompressCrypt tarCompressCrypt, CancellationTokenSource cancellationTokenSource, BenchmarkOptionHelper benchmarkOptionHelper)
        {
            _tarCompressCrypt = tarCompressCrypt;
            _cancellationTokenSource = cancellationTokenSource;
            _benchmarkOptionHelper = benchmarkOptionHelper;
        }

        private IEnumerable<BenchmarkIteration> GenerateBenchmarkIteration(BenchmarkOption benchmarkOption, IEnumerable<BenchmarkTestContent> testData)
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

        public async Task<OperationSummary> RunBenchmark(BenchmarkOption benchmarkOption)
        {
            var keysFolder = TestFileHelper.NewFolder();

            var data = await PrepareTestData(benchmarkOption);

            var iterations = GenerateBenchmarkIteration(benchmarkOption, data).ToList();

            var operationSummaries = new List<OperationSummary>();

            var threads = benchmarkOption.Threads == 0 ? Environment.ProcessorCount : benchmarkOption.Threads;

            foreach (var iteration in iterations)
            {
                PasswordMode pm = iteration.Encryption ? PasswordMode.PublicKey : PasswordMode.None;
                var compressedFolder = TestFileHelper.NewFolder(benchmarkOption.OutputCompressed);
                var outputFolder = TestFileHelper.NewFolder(benchmarkOption.OutputDecompressed);

                // compress
                var compressOption = new CompressOption
                {
                    Algo = iteration.Algo,
                    CompressionRatio = iteration.CompressionRatio,
                    BlockMode = BlockMode.Individual,
                    SourceDirOrFile = iteration.Content.Source,
                    DestinationDir = compressedFolder,
                    Threads = threads,
                    PasswordOption = await _benchmarkOptionHelper.GenerateCompressPasswordOption(pm, keysFolder)
                };
         
                OperationSummary resultCompress = await _tarCompressCrypt.Compress(compressOption);
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    await Cleanup();
                    return null;
                }
                operationSummaries.Add(resultCompress);
                resultCompress.ThrowOnError();

                // decompress
                var decompressOption = new DecompressOption
                {
                    SourceDirOrFile = compressedFolder,
                    DestinationDir = outputFolder,
                    Threads = threads,
                    PasswordOption = _benchmarkOptionHelper.GenerateDecompressPasswordOption(pm, keysFolder)
                };
             
                OperationSummary resultDecompress = await _tarCompressCrypt.Decompress(decompressOption);
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    await Cleanup();
                    return null;
                }

                operationSummaries.Add(resultDecompress);
                resultDecompress.ThrowOnError();

                StringBuilder sb = FormatResultSummary(iteration, resultCompress, resultDecompress);

                Console.Out.WriteLine(sb.ToString());

                async Task Cleanup()
                {
                    if (benchmarkOption.Cleanup)
                    {
                        await "del /f /s /q * > NUL".Run(compressedFolder, CancellationToken.None);
                        Directory.Delete(compressedFolder, true);
                        await "del /f /s /q * > NUL".Run(outputFolder, CancellationToken.None);
                        Directory.Delete(outputFolder, true);
                    }
                }

                await Cleanup();
            }

            return new OperationSummary(operationSummaries.SelectMany(i => i.OperationBlocks), 0, default);
        }

        private static StringBuilder FormatResultSummary(BenchmarkIteration iteration, OperationSummary resultCompress, OperationSummary resultDecompress)
        {
            string aes = iteration.Encryption ? "yes" : "no ";

            var statComp = resultCompress.Statistics;
            var statDecomp = resultDecompress.Statistics;

            var sb = new StringBuilder();
            sb.Append($"{iteration.Algo}".PadRight(6));
            sb.Append($"[{iteration.CompressionRatio.ToString().PadLeft(2)}] ");
            sb.Append($"aes:{aes} ");
            if (iteration.Content.Content != BenchmarkContent.UserDefined)
            {
                sb.Append($"{iteration.Content.Content.ToString().PadRight(6)} ");
            }
            sb.Append("compress ");
            sb.Append(statComp.AverageThroughput.HumanizedBandwidth().Pad(12));
            sb.Append($" [{resultCompress.Stopwatch.Elapsed.HumanizedTimeSpan().Pad(10)}] ");
            sb.Append("decompress ");
            sb.Append(statDecomp.AverageThroughput.HumanizedBandwidth().Pad(12));
            sb.Append($" [{resultDecompress.Stopwatch.Elapsed.HumanizedTimeSpan().Pad(10)}] ");
            sb.Append($"ratio:{resultCompress.CompressionRatio:0.####}");
            return sb;
        }
    }

    public class BenchmarkTestContent
    {
        public BenchmarkTestContent(string source, bool shouldDelete, BenchmarkContent content)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ShouldDelete = shouldDelete;
            Content = content;
        }

        public string Source { get; }
        public bool ShouldDelete { get; }
        public BenchmarkContent Content { get; }
    }
}