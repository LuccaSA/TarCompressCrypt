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
        public BenchmarkHelper(CancellationTokenSource cancellationTokenSource, TarCompressCrypt tarCompressCrypt)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _tarCompressCrypt = tarCompressCrypt;
        }

        private IEnumerable<BenchmarIteration> GenerateBenchmarkIteration()
        {
            var algos = new[] {
                CompressionAlgo.Lz4,
                CompressionAlgo.Brotli, 
                CompressionAlgo.Zstd
            };
            var withEncryption = new[] { false, true };

            foreach (CompressionAlgo algo in algos)
            {
                int ratioMax = MaxRatio(algo);

                foreach (bool encrypt in withEncryption)
                {
                    foreach (int ratio in Enumerable.Range(1, ratioMax))
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

        private static int MaxRatio(CompressionAlgo algo)
        {
            int ratioMax;
            switch (algo)
            {
                case CompressionAlgo.Lz4:
                    ratioMax = 9;
                    break;
                case CompressionAlgo.Brotli:
                    ratioMax = 9;
                    break;
                case CompressionAlgo.Zstd:
                    ratioMax = 19;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(algo));
            }
            return ratioMax;
        }

        public async Task<OperationSummary> RunBenchmark(BenchmarkOption benchmarkOption)
        {
            var keysFolder = TestFileHelper.NewFolder();
             
            FileInfo src = new FileInfo(benchmarkOption.Source);

            var iterations = GenerateBenchmarkIteration().ToList();

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

                foreach (var result in resultDecompress.CommandResults)
                {
                    result.ThrowOnError();
                }

                double sizeMb = src.Length / (double)(1024 * 1024);
                double compMbs = sizeMb / (swComp.ElapsedMilliseconds / (float)1000);
                double decompMbs = sizeMb / (swDecomp.ElapsedMilliseconds / (float)1000);

                Console.Out.WriteLine($"{iter.Algo} [{iter.CompressionRatio}] aes={iter.Encryption} : compress {compMbs:0.###} Mb/s, decompress {decompMbs:0.###} Mb/s, ratio {compressionFactor:0.###}");
            }

            return new OperationSummary(null, null);
        }
    }
}