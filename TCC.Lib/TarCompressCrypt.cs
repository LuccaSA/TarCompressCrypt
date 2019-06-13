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
using TCC.Lib.Database;
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
        private readonly Database.Database _db;

        public TarCompressCrypt(CancellationTokenSource cancellationTokenSource, IBlockListener blockListener, ILogger<TarCompressCrypt> logger, EncryptionCommands encryptionCommands, CompressionCommands compressionCommands, Database.Database db)
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
            IEnumerable<CompressionBlock> blocks = BlockHelper.PrepareCompressBlocks(option);
            IEnumerable<CompressionBlock> ordered = await PrepareCompressionBlocksAsync(blocks);

            var job = new BackupJob
            {
                StartTime = DateTime.UtcNow,
                BlockJobs = new List<BackupBlockJob>()
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
                    return new OperationCompressionBlock(block, result);
                }, po)
                // Cleanup loop
                .ParallelizeStreamAsync(async (opb, token) =>
                {
                    await _encryptionCommands.CleanupKey(opb.Block, option, opb.CommandResult, Mode.Compress);
                    return opb;
                }, po)
                .ForEachAsync((i, ct) =>
                {
                    _blockListener.OnBlockReport(new CompressionBlockReport(i.Item.CommandResult, i.Item.CompressionBlock, counter.Count));
                    return Task.CompletedTask;
                })
                .AsReadOnlyCollection();

            job.BlockJobs = operationBlocks.Select(i => new BackupBlockJob
            {
                StartTime = i.CompressionBlock.StartTime,
                FullSourcePath = i.CompressionBlock.SourceFileOrDirectory.FullPath,
                Duration = TimeSpan.FromMilliseconds(i.CommandResult.ElapsedMilliseconds),
                Size = i.CompressionBlock.CompressedSize,
                Exception = i.CommandResult.Errors,
                Success = i.CommandResult.IsSuccess
            }).ToList();

            sw.Stop();
            job.Duration = sw.Elapsed;
            var db = await _db.GetDbAsync();
            db.BackupJobs.Add(job);
            db.BackupBlockJobs.AddRange(job.BlockJobs);
            await db.SaveChangesAsync();
            var ops = new OperationSummary(operationBlocks, option.Threads, sw);
            return ops;
        }

        private async Task<IEnumerable<CompressionBlock>> PrepareCompressionBlocksAsync(IEnumerable<CompressionBlock> blocks)
        {
            var db = await _db.GetDbAsync();
            var jobs = await db.BackupJobs
                .Include(i => i.BlockJobs)
                .LastOrDefaultAsync();

            if (jobs?.BlockJobs == null || jobs.BlockJobs.Count == 0)
            {
                // no history ATM, we consider a backup full for each block
                return blocks.Foreach(b => { b.BackupMode = BackupMode.Full; });
            }

            var sequence = jobs.BlockJobs.OrderByDescending(b => b.Size);

            return blocks.OrderBySequence(sequence,
                b => b.SourceFileOrDirectory.FullPath,
                p => p.FullSourcePath,
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
                b.DiffDate = p.StartTime; // diff since the last full or diff
            });
        }

        public async Task<OperationSummary> Decompress(DecompressOption option)
        {
            IEnumerable<DecompressionBlock> blocks = BlockHelper.PrepareDecompressBlocks(option);
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
                    return new OperationDecompressionsBlock(block, result);
                }, po)
                // Cleanup loop
                .ParallelizeStreamAsync(async (opb, token) =>
                {
                    await _encryptionCommands.CleanupKey(opb.Block, option, opb.CommandResult, Mode.Compress);
                    return opb;
                }, po)
                .ForEachAsync((i, ct) =>
                {
                    _blockListener.OnBlockReport(new BlockReport(i.Item.CommandResult, counter.Count, i.Item.Block));
                    return Task.CompletedTask;
                })
                .AsReadOnlyCollection();

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