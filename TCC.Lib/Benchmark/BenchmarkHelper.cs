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
    public static class BenchmarkHelper
    {
        private static IEnumerable<BenchmarIteration> GenerateBenchmarkIteration(this BenchmarkOption benchmarkOption)
        {
            var algos = new[] {
                CompressionAlgo.Lz4,
                CompressionAlgo.Brotli, 
                CompressionAlgo.Zstd
            };
            var withEncryption = new[] { false, true };

            foreach (CompressionAlgo algo in algos)
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
                        throw new ArgumentOutOfRangeException();
                }

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

        public static async Task<OperationSummary> RunBenchmark(BenchmarkOption benchmarkOption, CancellationToken cts)
        {

            //var inputFolder = TestFileHelper.NewFolder();

            var keysFolder = TestFileHelper.NewFolder();

            //var testFile = TestFileHelper.NewFile(inputFolder, fileSizeMb);
            FileInfo src = new FileInfo(benchmarkOption.Source);

            var iterations = benchmarkOption.GenerateBenchmarkIteration().ToList();

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

                var resultCompress = await TarCompressCrypt.Compress(compressOption, cancellationToken: cts);

                swComp.Stop();
                if (!resultCompress.IsSuccess)
                {
                    throw new Exception();
                }

                var compressedFile = resultCompress.Blocks.Select(i => i.DestinationArchive).FirstOrDefault();
                FileInfo fi = new FileInfo(compressedFile);

                double compressionFactor = (double)src.Length / (double)fi.Length;

                // decompress
                var decompressOption = new DecompressOption
                {
                    SourceDirOrFile = compressedFile,
                    DestinationDir = outputFolder,
                    Threads = Environment.ProcessorCount,
                    PasswordOption = BenchmarkOptionHelper.GenerateDecompressPasswordOption(pm, keysFolder)
                };

                Stopwatch swDecomp = Stopwatch.StartNew();
                var resultDecompress = await TarCompressCrypt.Decompress(decompressOption, cancellationToken: cts);
                swDecomp.Stop();
                if (!resultDecompress.IsSuccess)
                {
                    throw new Exception();
                }

                double sizeMb = src.Length / (double)(1024 * 1024);
                double comp_Mbs = sizeMb / (swComp.ElapsedMilliseconds / (float)1000);
                double decomp_Mbs = sizeMb / (swDecomp.ElapsedMilliseconds / (float)1000);

                Console.Out.WriteLine($"{iter.Algo} [{iter.CompressionRatio}] aes={iter.Encryption} : compress {comp_Mbs:0.###} Mb/s, decompress {decomp_Mbs:0.###} Mb/s, ratio {compressionFactor:0.###}");
            }

            return new OperationSummary(null, null);
        }
    }
}