using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Blocks;
using TCC.Lib.Command;
using TCC.Lib.Database;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Benchmark
{
    public class BenchmarkRunner
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly TarCompressCrypt _tarCompressCrypt;
        private readonly BenchmarkOptionHelper _benchmarkOptionHelper;
        private readonly BenchmarkIterationGenerator _iterationGenerator;

        public BenchmarkRunner(TarCompressCrypt tarCompressCrypt, CancellationTokenSource cancellationTokenSource, BenchmarkOptionHelper benchmarkOptionHelper, BenchmarkIterationGenerator iterationGenerator)
        {
            _tarCompressCrypt = tarCompressCrypt;
            _cancellationTokenSource = cancellationTokenSource;
            _benchmarkOptionHelper = benchmarkOptionHelper;
            _iterationGenerator = iterationGenerator;
        }

        public async Task<OperationSummary> RunBenchmarkAsync(BenchmarkOption benchmarkOption)
        {
            var operationSummaries = new List<OperationSummary>();
            var keysFolder = TestFileHelper.NewFolder();

            var iterations = await _iterationGenerator.PrepareIteration(benchmarkOption);
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
                    PasswordOption = await _benchmarkOptionHelper.GenerateCompressPasswordOption(pm, keysFolder),
                    BackupMode = BackupMode.Full
                };
                OperationSummary resultCompress = await _tarCompressCrypt.CompressAsync(compressOption);

                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    await Cleanup();
                    return null;
                }
                operationSummaries.Add(resultCompress);
                resultCompress.ThrowOnError();

                // decompress
                var rootForDecompression = new DirectoryInfo(compressedFolder)
                    .EnumerateDirectories(Environment.MachineName, SearchOption.TopDirectoryOnly)
                    .First();
                var decompressOption = new DecompressOption
                {
                    SourceDirOrFile = rootForDecompression.FullName,
                    DestinationDir = outputFolder,
                    Threads = threads,
                    PasswordOption = _benchmarkOptionHelper.GenerateDecompressPasswordOption(pm, keysFolder)
                };
                OperationSummary resultDecompress = await _tarCompressCrypt.DecompressAsync(decompressOption);
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
            return new OperationSummary(operationSummaries.SelectMany(i => i.OperationBlocks), 0, default, benchmarkOption.Source);
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
}