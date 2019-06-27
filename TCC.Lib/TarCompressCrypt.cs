using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            IEnumerable<CompressionBlock> blocks = option.GenerateCompressBlocks();
            var ordered = PrepareCompressionBlocksAsync(blocks);

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
                    b.StartTime = DateTime.UtcNow;
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
                    await _encryptionCommands.CleanupKey(opb.BlockResults.First().Block, option, opb.BlockResults.First().CommandResult, Mode.Compress);
                    return opb;
                }, po)
                .ForEachAsync((i, ct) =>
                {
                    _blockListener.OnBlockReport(new CompressionBlockReport(i.Item.BlockResults.First().CommandResult, i.Item.CompressionBlock, counter.Count));
                    return Task.CompletedTask;
                })
                .AsReadOnlyCollection();

            job.BlockJobs = operationBlocks.Select(i => new BackupBlockJob
            {
                StartTime = i.CompressionBlock.StartTime,
                FullSourcePath = i.CompressionBlock.SourceFileOrDirectory.FullPath,
                Duration = TimeSpan.FromMilliseconds(i.BlockResults.First().CommandResult.ElapsedMilliseconds),
                Size = i.CompressionBlock.CompressedSize,
                Exception = i.BlockResults.First().CommandResult.Errors,
                Success = i.BlockResults.First().CommandResult.IsSuccess
            }).ToList();

            sw.Stop();
            job.Duration = sw.Elapsed;
            var db = await _db.BackupDbAsync();
            db.BackupJobs.Add(job);
            db.BackupBlockJobs.AddRange(job.BlockJobs);
            await db.SaveChangesAsync();
            var ops = new OperationSummary(operationBlocks, option.Threads, sw);
            return ops;
        }

        public async Task<OperationSummary> Decompress(DecompressOption option)
        {
            IEnumerable<DecompressionBatch> blocks = option.GenerateDecompressBlocks();
            IAsyncEnumerable<DecompressionBatch> ordered = PrepareDecompressionBlocksAsync(blocks);

            var sw = Stopwatch.StartNew();
            var po = ParallelizeOption(option);

            IReadOnlyCollection<OperationDecompressionsBlock> operationBlocks = 
                await ordered
                .AsAsyncStream(_cancellationTokenSource.Token)
                .CountAsync(out var counter)
                // Prepare decryption
                .ParallelizeStreamAsync(async (b, token) =>
                {
                    await _encryptionCommands.PrepareDecryptionKey(b.BackupFull, option, token);
                    return b;
                }, po)
                // Core loop 
                .ParallelizeStreamAsync(async (batch, token) =>
                {
                    var blockResults = new List<BlockResult>();
                    if (batch.BackupFull != null)
                    {
                        var dblock = await DecompressBlock(option, batch.BackupFull, token);
                        blockResults.Add(new BlockResult(batch.BackupFull, dblock));
                    }
                    if (batch.BackupsDiff != null)
                    {
                        foreach (var block in batch.BackupsDiff)
                        {
                            var dblock = await DecompressBlock(option, block, token);
                            blockResults.Add(new BlockResult(block, dblock));
                        }
                    }

                    return new OperationDecompressionsBlock(blockResults);
                }, po)
                // Cleanup loop
                .ParallelizeStreamAsync(async (opb, token) =>
                {
                    foreach (var b in opb.BlockResults)
                    {
                        await _encryptionCommands.CleanupKey(b.Block, option, b.CommandResult, Mode.Compress);
                    }
                    return opb;
                }, po)
                .ForEachAsync((i, ct) =>
                {
                    foreach (var b in i.Item.BlockResults)
                    {
                        _blockListener.OnBlockReport(new BlockReport(b.CommandResult, counter.Count, b.Block));
                    }

                    return Task.CompletedTask;
                })
                .AsReadOnlyCollection();

            sw.Stop();
            return new OperationSummary(operationBlocks, option.Threads, sw);
        }

        private async Task<CommandResult> DecompressBlock(DecompressOption option, DecompressionBlock block, CancellationToken token)
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
            return result;
        }

        private async IAsyncEnumerable<CompressionBlock> PrepareCompressionBlocksAsync(IEnumerable<CompressionBlock> blocks)
        {
            var db = await _db.BackupDbAsync();
            var jobs = await db.BackupJobs
                .OrderByDescending(i => i.StartTime)
                .Include(i => i.BlockJobs)
                .FirstOrDefaultAsync();

            if (jobs?.BlockJobs == null || jobs.BlockJobs.Count == 0)
            {
                // no history ATM, we consider a backup full for each block
                foreach (var b in blocks)
                {
                    b.BackupMode = BackupMode.Full;
                    yield return b;
                }
                yield break;
            }

            var blocksInHistory = jobs.BlockJobs.OrderByDescending(b => b.Size);

            await foreach (var b in blocks.OrderBySequence(blocksInHistory,
                b => b.SourceFileOrDirectory.FullPath,
                p => p.FullSourcePath,
                async (b, p) =>
                {
                    // If already Full here, it's a request from command line
                    if (b.BackupMode.HasValue && b.BackupMode.Value == BackupMode.Full)
                    {
                        return; // we respect command line
                    }

                    var lastBackup = await db.BackupBlockJobs
                        .Where(i => i.FullSourcePath == b.SourceFileOrDirectory.FullPath)
                        .OrderByDescending(i => i.StartTime)
                        .FirstOrDefaultAsync();

                    if (lastBackup != null)
                    {
                        // If last backup found, we plan a backup diff
                        b.BackupMode = BackupMode.Diff;
                        b.DiffDate = lastBackup.StartTime;
                    }
                    else
                    {
                        // No previous backup, we start with a full
                        b.BackupMode = BackupMode.Full;
                    }
                }))
            {
                yield return b;
            }
        }

        private async IAsyncEnumerable<DecompressionBatch> PrepareDecompressionBlocksAsync(IEnumerable<DecompressionBatch> blocks)
        {
            var db = await _db.RestoreDbAsync();
            var jobs = await db.RestoreJobs
                .Include(i => i.BlockJobs)
                .LastOrDefaultAsync();

            if (jobs?.BlockJobs == null || jobs.BlockJobs.Count == 0)
            {
                // no history ATM, we consider a restore FULL + DIFF for each block
                foreach (var b in blocks)
                {
                    yield return b;
                }
                yield break;
            }
            
            foreach (DecompressionBatch decompBlock in blocks.OrderByDescending(i => i.Size))
            {
                var opFolder = (decompBlock.BackupFull ?? decompBlock.BackupsDiff.First()).OperationFolder;

                var lastFull = await db.RestoreBlockJobs
                    .Where(i => i.FullDestinationPath == opFolder && i.BackupMode == BackupMode.Full)
                    .OrderByDescending(i => i.StartTime)
                    .FirstOrDefaultAsync();

                if (lastFull == null)
                {
                    if (decompBlock.BackupFull == null)
                    {
                        throw new Exception("missing backup full");
                    }

                    // no backup full in history : we decompress the FULL + DIFF
                    yield return decompBlock;
                    continue;
                }

                var dir = new DirectoryInfo(decompBlock.BackupFull.OperationFolder);
                if (!dir.Exists || !dir.EnumerateFiles().Any())
                {
                    // we have a backup full, but we need to check if target folder complies :
                    // - check if exists
                    // - check if empty
                    yield return decompBlock;
                    continue;
                }

                var lastDiffSinceFull = await db.RestoreBlockJobs
                    .Where(i => i.FullDestinationPath == opFolder && i.BackupMode == BackupMode.Diff && i.StartTime > lastFull.StartTime)
                    .OrderByDescending(i => i.StartTime)
                    .FirstOrDefaultAsync();

                if (lastDiffSinceFull == null)
                {
                    // No diff in history, we send all the existing diff EXCEPT the full
                    yield return new DecompressionBatch
                    {
                        BackupsDiff = decompBlock.BackupsDiff
                    };
                }
                else
                {
                    // at least a DIFF in history, since the last FULL
                    // we decompress the DIFF delta (since the last DIFF)
                    yield return new DecompressionBatch
                    {
                        BackupsDiff = decompBlock.BackupsDiff.Where(i => i.BackupDate >= lastDiffSinceFull.StartTime).ToList()
                    };
                }
            }
            
            // TODO next
            // - si on déclenche un restore d'un FULL sur un dossier ou y'a deja des données, faut cleaner avant
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