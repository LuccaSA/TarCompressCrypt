using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TCC.Lib.AsyncStreams;
using TCC.Lib.Blocks;
using TCC.Lib.Command;
using TCC.Lib.Dependencies;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib
{
    public class TarCompressCrypt
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly IBlockListener _blockListener;
        private readonly ILogger<TarCompressCrypt> _logger;
        private readonly EncryptionCommands _encryptionCommands;
        private readonly CompressionCommands _compressionCommands;
        private readonly Database _db;

        public TarCompressCrypt(CancellationTokenSource cancellationTokenSource, IBlockListener blockListener, ILogger<TarCompressCrypt> logger, EncryptionCommands encryptionCommands, CompressionCommands compressionCommands, Database db)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _blockListener = blockListener;
            _logger = logger;
            _encryptionCommands = encryptionCommands;
            _compressionCommands = compressionCommands;
            _db = db;
        }

        public async Task<OperationSummary> Compress(CompressOption option)
        {
            var sw = Stopwatch.StartNew();

            var po = ParallelizeOption(option);
            IEnumerable<Block> blocks = BlockHelper.PreprareCompressBlocks(option);
            IEnumerable<Block> ordered = await PrepareCompressionBlocksAsync(blocks);
            var job = new Job
            {
                StartTime = DateTime.UtcNow,
                BlockJobs = new List<BlockJob>()
            };

            var operationBlocks = await ordered
                .AsAsyncStream(_cancellationTokenSource.Token)
                .CountAsync(out var counter)
                // Prepare encyption
                .ParallelizeStreamAsync(async (b, token) =>
                {
                    await _encryptionCommands.PrepareEncryptionKey(b, option, token);
                    return b;
                }, po)
                // Core loop 
                .ParallelizeStreamAsync(async (block, token) =>
                {
                    _logger.LogInformation($"Starting {block.Source}");
                    CommandResult result = null;
                    try
                    {
                        string cmd = _compressionCommands.CompressCommand(block, option);
                        result = await cmd.Run(block.OperationFolder, token);
                        _logger.LogInformation($"Finished {block.Source} on {result?.ElapsedMilliseconds} ms");
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, $"Error on {block.Source}");
                    }
                    return new OperationBlock(block, result);
                }, po)
                // Cleanup loop
                .ParallelizeStreamAsync(async (opb, token) =>
                {
                    await _encryptionCommands.CleanupKey(opb.Block, option, opb.CommandResult, Mode.Compress);
                    return opb;
                }, po)
                .ForEachAsync((i, ct) =>
                {
                    _blockListener.OnBlockReport(new BlockReport(i.Item.CommandResult, i.Item.Block, counter.Count));
                    return Task.CompletedTask;
                })
                .AsEnumerableAsync();

            job.BlockJobs = operationBlocks.Select(i => new BlockJob
            {
                Source = i.Block.Source,
                Duration = TimeSpan.FromMilliseconds(i.CommandResult.ElapsedMilliseconds),
                Size = i.Block.TargetSize,
                Exception = i.CommandResult.Errors,
                Success = i.CommandResult.IsSuccess
            }).ToList();
            sw.Stop();
            job.Duration = sw.Elapsed;
            var db = await _db.GetDbAsync();
            db.Jobs.Add(job);
            db.BlockJobs.AddRange(job.BlockJobs);
            await db.SaveChangesAsync();
            var ops = new OperationSummary(operationBlocks, option.Threads, sw);
            return ops;
        }

        private async Task<IEnumerable<Block>> PrepareCompressionBlocksAsync(IEnumerable<Block> blocks)
        {
            var db = await _db.GetDbAsync();
            var jobs = await db.Jobs
                .Include(i => i.BlockJobs)
                .LastOrDefaultAsync();

            if (jobs?.BlockJobs == null || jobs.BlockJobs.Count == 0)
            {
                return blocks;
            }

            var sequence = jobs.BlockJobs.OrderByDescending(b => b.Size);

            return blocks.OrderBySequence(sequence,
                b => b.Source,
                p => p.Source,
                (b, p) =>
            {
                // If already Full here, it's a request from command line
                if (b.BackupMode.HasValue && b.BackupMode.Value == BackupMode.Full)
                {
                    return; // we respect command line
                }

                // If previous backup exists, then we will do a backup diff
                // If no previous backup, we do a backup full
                b.BackupMode = p != null ? BackupMode.Diff : BackupMode.Full;
            });
        }

        public async Task<OperationSummary> Decompress(DecompressOption option)
        {
            IEnumerable<Block> blocks = BlockHelper.PreprareDecompressBlocks(option);
            var sw = Stopwatch.StartNew();
            var po = ParallelizeOption(option);

            var operationBlocks = await blocks
                .AsAsyncStream(_cancellationTokenSource.Token)
                .CountAsync(out var counter)
                // Prepare decryption
                .ParallelizeStreamAsync(async (b, token) =>
                {
                    await _encryptionCommands.PrepareDecryptionKey(b, option, token);
                    return b;
                }, po)
                // Core loop 
                .ParallelizeStreamAsync(async (block, token) =>
                {
                    _logger.LogInformation($"Starting {block.Source}");
                    CommandResult result = null;
                    try
                    {
                        string cmd = _compressionCommands.DecompressCommand(block, option);
                        result = await cmd.Run(block.OperationFolder, token);
                        _logger.LogInformation($"Finished {block.Source} on {result?.ElapsedMilliseconds} ms");
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, $"Error on {block.Source}");
                    }
                    return new OperationBlock(block, result);
                }, po)
                // Cleanup loop
                .ParallelizeStreamAsync(async (opb, token) =>
                {
                    await _encryptionCommands.CleanupKey(opb.Block, option, opb.CommandResult, Mode.Compress);
                    return opb;
                }, po)
                .ForEachAsync((i, ct) =>
                {
                    _blockListener.OnBlockReport(new BlockReport(i.Item.CommandResult, i.Item.Block, counter.Count));
                    return Task.CompletedTask;
                })
                .AsEnumerableAsync();

            sw.Stop();
            return new OperationSummary(operationBlocks, option.Threads, sw);
        }


        private static ParallelizeOption ParallelizeOption(TccOption option)
        {
            var po = new ParallelizeOption
            {
                FailMode = option.FailFast ? Fail.Fast : Fail.Smart,
                MaxDegreeOfParallelism = option.Threads
            };
            return po;
        }

    }
}