using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Blocks;
using TCC.Lib.Options;

namespace TCC.Lib.Benchmark
{
    public class BenchmarkHelper
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly TarCompressCrypt _tarCompressCrypt;
        public BenchmarkHelper(TarCompressCrypt tarCompressCrypt, CancellationTokenSource cancellationTokenSource)
        {
            _tarCompressCrypt = tarCompressCrypt;
            _cancellationTokenSource = cancellationTokenSource;
        }

        private IEnumerable<BenchmarIteration> GenerateBenchmarkIteration(BenchmarkOption benchmarkOption, IEnumerable<BenchmarkTestContent> testData)
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
                            yield return new BenchmarIteration
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

       

        private List<BenchmarkTestContent> PrepareTestData(BenchmarkOption benchmarkOption)
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
                foreach (int i in Enumerable.Range(0, benchmarkOption.NumberOfFiles))
                {
                    TestFileHelper.NewFile(testAsciiDataFolder, benchmarkOption.FileSize, true);
                }
                list.Add(new BenchmarkTestContent(testAsciiDataFolder, true, BenchmarkContent.Ascii));
            }

            if ((benchmarkOption.Content & BenchmarkContent.Binary) == BenchmarkContent.Binary)
            {
                var testBinaryDataFolder = TestFileHelper.NewFolder();
                foreach (int i in Enumerable.Range(0, benchmarkOption.NumberOfFiles))
                {
                    TestFileHelper.NewFile(testBinaryDataFolder, benchmarkOption.FileSize, false);
                }
                list.Add(new BenchmarkTestContent(testBinaryDataFolder, true, BenchmarkContent.Binary));
            }
            return list;
        }

        private static IEnumerable<int> GetBenchmarkRatios(BenchmarkOption benchmarkOption, CompressionAlgo algo)
        {
            if (benchmarkOption.Ratio == 0)
                return Enumerable.Range(1, MaxRatio(algo));
            else
                return new[] { benchmarkOption.Ratio };
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

            var data = PrepareTestData(benchmarkOption);

            var iterations = GenerateBenchmarkIteration(benchmarkOption, data).ToList();

            var operationSummaries = new List<OperationSummary>();

            var threads = benchmarkOption.Threads == 0 ? Environment.ProcessorCount : benchmarkOption.Threads;

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
                    BlockMode = BlockMode.Individual,
                    SourceDirOrFile = iter.Content.Source,
                    DestinationDir = compressedFolder,
                    Threads = threads,
                    PasswordOption = await BenchmarkOptionHelper.GenerateCompressPassswordOption(pm, keysFolder)
                };

                var swComp = Stopwatch.StartNew();
                OperationSummary resultCompress = await _tarCompressCrypt.Compress(compressOption);
                swComp.Stop();
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    return null;
                }

                operationSummaries.Add(resultCompress);
                foreach (var result in resultCompress.CommandResults)
                {
                    result.ThrowOnError();
                }

                var compressedSize = resultCompress.Blocks.Select(i => new FileInfo(i.DestinationArchive))
                    .Select(f => f.Length)
                    .Sum();

                double compressionFactor = iter.Content.Size / (double)compressedSize;

                // decompress
                var decompressOption = new DecompressOption
                {
                    SourceDirOrFile = compressedFolder,
                    DestinationDir = outputFolder,
                    Threads = threads,
                    PasswordOption = BenchmarkOptionHelper.GenerateDecompressPasswordOption(pm, keysFolder)
                };
                var swDecomp = Stopwatch.StartNew();
                OperationSummary resultDecompress = await _tarCompressCrypt.Decompress(decompressOption);
                swDecomp.Stop();
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    return null;
                }

                operationSummaries.Add(resultDecompress);

                foreach (var result in resultDecompress.CommandResults)
                {
                    result.ThrowOnError();
                }

                double sizeMb = iter.Content.Size / (double)(1024 * 1024);
                double compMbs = sizeMb / (swComp.ElapsedMilliseconds / (float)1000);
                double decompMbs = sizeMb / (swDecomp.ElapsedMilliseconds / (float)1000);

                string aes = iter.Encryption ? "yes" : "no ";

                Console.Out.WriteLine($"{iter.Algo} [{iter.CompressionRatio}] aes:{aes} data:{iter.Content.Content} compress {compMbs:0.###} Mb/s, decompress {decompMbs:0.###} Mb/s, ratio {compressionFactor:0.###}");
            }

            return new OperationSummary(operationSummaries.SelectMany(i => i.Blocks), operationSummaries.SelectMany(i => i.CommandResults));
        }
    }

    public class BenchmarkTestContent
    {
        private long _size;

        public BenchmarkTestContent(string source, bool shouldDelete, BenchmarkContent content)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ShouldDelete = shouldDelete;
            Content = content;
        }

        public string Source { get; }
        public bool ShouldDelete { get; }
        public BenchmarkContent Content { get; }

        public long Size
        {
            get
            {
                if (_size == 0)
                {
                    if (File.GetAttributes(Source).HasFlag(FileAttributes.Directory))
                    {
                        var dir = new DirectoryInfo(Source);
                        _size = dir.GetFiles().Select(f => f.Length).Sum();
                    }
                    else
                    {
                        _size = new FileInfo(Source).Length;
                    }
                }
                return _size;
            } 
        }
    }
}