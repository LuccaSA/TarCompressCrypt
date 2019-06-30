using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceProvider _serviceProvider;

        public TarCompressCrypt(CancellationTokenSource cancellationTokenSource, IBlockListener blockListener, ILogger<TarCompressCrypt> logger, EncryptionCommands encryptionCommands, CompressionCommands compressionCommands, Database.Database db, IServiceProvider serviceProvider)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _blockListener = blockListener;
            _logger = logger;
            _encryptionCommands = encryptionCommands;
            _compressionCommands = compressionCommands;
            _db = db;
            _serviceProvider = serviceProvider;
        }

        public async Task<OperationSummary> Compress(CompressOption option)
        {
            var sw = Stopwatch.StartNew();
            var po = ParallelizeOption(option);
            int idBackupJob = await InitializeBackupJobAsync();

            IEnumerable<CompressionBlock> blocks = option.GenerateCompressBlocks();
            var ordered = PrepareCompressionBlocksAsync(blocks, idBackupJob);

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
                    var opb = new OperationCompressionBlock(block, result);
                    await AddBackupBlockJobAsync(opb, idBackupJob);
                    return opb;
                }, po)
                // Cleanup loop
                .ParallelizeStreamAsync(async (opb, token) =>
                {
                    await _encryptionCommands.CleanupKey(opb.BlockResults.First().Block, option, opb.BlockResults.First().CommandResult, Mode.Compress);
                    return opb;
                }, po)
                .ForEachAsync(async (i, ct) =>
                {
                    await _blockListener.OnCompressionBlockReportAsync(new CompressionBlockReport(i.Item.BlockResults.First().CommandResult, i.Item.CompressionBlock, counter.Count));
                })
                .AsReadOnlyCollectionAsync();

            sw.Stop();

            await UpdateBackupJobStatsAsync(sw, idBackupJob);
            _blockListener.Complete();
            var ops = new OperationSummary(operationBlocks, option.Threads, sw);
            return ops;
        }


        public async Task<OperationSummary> Decompress(DecompressOption option)
        {
            var sw = Stopwatch.StartNew();
            var po = ParallelizeOption(option);
            int idRestoreJob = await InitializeRestoreJobAsync();

            IEnumerable<DecompressionBatch> blocks = option.GenerateDecompressBlocks();
            IAsyncEnumerable<DecompressionBatch> ordered = PrepareDecompressionBlocksAsync(blocks, idRestoreJob);

            IReadOnlyCollection<OperationDecompressionsBlock> operationBlocks =
                await ordered
                .AsAsyncStream(_cancellationTokenSource.Token)
                .CountAsync(out var counter)
                // Prepare decryption
                .ParallelizeStreamAsync(async (b, token) =>
                {
                    b.StartTime = DateTime.UtcNow;
                    await _encryptionCommands.PrepareDecryptionKey(b.BackupFull, option, token);
                    return b;
                }, po)
                // Core loop 
                .ParallelizeStreamAsync(async (batch, token) =>
                {
                    var blockResults = new List<BlockResult>();
                    if (batch.BackupFull != null)
                    {
                        batch.BackupFullCommandResult = await DecompressBlock(option, batch.BackupFull, token);
                        blockResults.Add(new BlockResult(batch.BackupFull, batch.BackupFullCommandResult));
                    }
                    if (batch.BackupsDiff != null)
                    {
                        batch.BackupDiffCommandResult = new CommandResult[batch.BackupsDiff.Length];
                        for (int i = 0; i < batch.BackupsDiff.Length; i++)
                        {
                            batch.BackupDiffCommandResult[i] = await DecompressBlock(option, batch.BackupsDiff[i], token);
                            blockResults.Add(new BlockResult(batch.BackupsDiff[i], batch.BackupDiffCommandResult[i]));
                        }
                    }
                    var odb = new OperationDecompressionsBlock(blockResults, batch);
                    await AddRestoreBlockJobAsync(odb, idRestoreJob);
                    return odb;
                }, po)
                // Cleanup loop
                .ParallelizeStreamAsync(async (odb, token) =>
                {
                    foreach (var b in odb.BlockResults)
                    {
                        await _encryptionCommands.CleanupKey(b.Block, option, b.CommandResult, Mode.Compress);
                    }
                    return odb;
                }, po)
                .ForEachAsync(async (i, ct) =>
                {
                    await _blockListener.OnDecompressionBatchReportAsync(new DecompressionBlockReport(i.Item.Batch, counter.Count));
                })
                .AsReadOnlyCollectionAsync();

            sw.Stop();
            await UpdateRestoreJobStatsAsync(sw, idRestoreJob);
            _blockListener.Complete();
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

        private async IAsyncEnumerable<CompressionBlock> PrepareCompressionBlocksAsync(
            IEnumerable<CompressionBlock> blocks, int currentBackupJobId)
        {
            var db = await _db.BackupDbAsync();
            var jobs = await db.BackupJobs
                .Where(i => i.Id != currentBackupJobId)
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

        private async IAsyncEnumerable<DecompressionBatch> PrepareDecompressionBlocksAsync(
            IEnumerable<DecompressionBatch> blocks, int currentRestoreJobId)
        {
            var db = await _db.RestoreDbAsync();
            var jobs = await db.RestoreJobs
                .Where(i => i.Id != currentRestoreJobId)
                .OrderByDescending(i => i.StartTime)
                .Include(i => i.BlockJobs)
                .FirstOrDefaultAsync();

            if (jobs?.BlockJobs == null || jobs.BlockJobs.Count == 0)
            {
                // no history ATM, we consider a restore FULL + DIFF for each block
                foreach (var b in blocks)
                {
                    yield return b;
                }
                yield break;
            }

            foreach (DecompressionBatch decompBlock in blocks.OrderByDescending(i => i.CompressedSize))
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
                        BackupsDiff = decompBlock.BackupsDiff.Where(i => i.BackupDate >= lastDiffSinceFull.StartTime).ToArray()
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

        private async Task<int> InitializeBackupJobAsync()
        {
            int idBackupJob;
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = await scope.ServiceProvider.GetRequiredService<Database.Database>().BackupDbAsync();
                var job = new BackupJob
                {
                    StartTime = DateTime.UtcNow,
                    BlockJobs = new List<BackupBlockJob>()
                };
                db.BackupJobs.Add(job);
                await db.SaveChangesAsync();
                idBackupJob = job.Id;
            }
            return idBackupJob;
        }

        private async Task UpdateBackupJobStatsAsync(Stopwatch sw, int idBackupJob)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = await scope.ServiceProvider.GetRequiredService<Database.Database>().BackupDbAsync();
                var job = await db.BackupJobs.FirstOrDefaultAsync(i => i.Id == idBackupJob);
                job.Duration = sw.Elapsed;
                await db.SaveChangesAsync();
            }
        }

        private async Task AddBackupBlockJobAsync(OperationCompressionBlock ocb, int idBackupJob)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = await scope.ServiceProvider.GetRequiredService<Database.Database>().BackupDbAsync();
                var job = await db.BackupJobs.FirstOrDefaultAsync(i => i.Id == idBackupJob);
                var bbj = new BackupBlockJob
                {
                    JobId = idBackupJob,
                    StartTime = ocb.CompressionBlock.StartTime,
                    FullSourcePath = ocb.CompressionBlock.SourceFileOrDirectory.FullPath,
                    Duration = TimeSpan.FromMilliseconds(ocb.BlockResults.First().CommandResult.ElapsedMilliseconds),
                    Size = ocb.CompressionBlock.CompressedSize,
                    Exception = ocb.BlockResults.First().CommandResult.Errors,
                    Success = ocb.BlockResults.First().CommandResult.IsSuccess
                };
                db.BackupBlockJobs.Add(bbj);
                await db.SaveChangesAsync();
            }
        }

        private async Task<int> InitializeRestoreJobAsync()
        {
            int idRestoreJob;
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = await scope.ServiceProvider.GetRequiredService<Database.Database>().RestoreDbAsync();
                var job = new RestoreJob
                {
                    StartTime = DateTime.UtcNow,
                    BlockJobs = new List<RestoreBlockJob>()
                };
                db.RestoreJobs.Add(job);
                await db.SaveChangesAsync();
                idRestoreJob = job.Id;
            }
            return idRestoreJob;
        }

        private async Task UpdateRestoreJobStatsAsync(Stopwatch sw, int idBackupJob)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = await scope.ServiceProvider.GetRequiredService<Database.Database>().RestoreDbAsync();
                var job = await db.RestoreJobs.FirstOrDefaultAsync(i => i.Id == idBackupJob);
                job.Duration = sw.Elapsed;
                await db.SaveChangesAsync();
            }
        }

        private async Task AddRestoreBlockJobAsync(OperationDecompressionsBlock ocb, int idBackupJob)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = await scope.ServiceProvider.GetRequiredService<Database.Database>().RestoreDbAsync();
                var job = await db.RestoreJobs.FirstOrDefaultAsync(i => i.Id == idBackupJob);
                var rbj = new RestoreBlockJob
                {
                    JobId = idBackupJob,
                    StartTime = ocb.Batch.StartTime,
                    FullDestinationPath = ocb.Batch.DestinationFolder,
                    Duration = TimeSpan.FromMilliseconds(ocb.BlockResults.First().CommandResult.ElapsedMilliseconds),
                    Size = ocb.Batch.CompressedSize,
                    Exception = ocb.BlockResults.First().CommandResult.Errors,
                    Success = ocb.BlockResults.First().CommandResult.IsSuccess
                };
                db.RestoreBlockJobs.Add(rbj);
                await db.SaveChangesAsync();
            }
        }
    }
}