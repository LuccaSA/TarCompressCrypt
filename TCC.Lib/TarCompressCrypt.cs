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
using System.Threading.Channels;
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
        private readonly IServiceProvider _serviceProvider;
        private readonly DatabaseHelper _databaseHelper;

        public TarCompressCrypt(CancellationTokenSource cancellationTokenSource, IBlockListener blockListener, ILogger<TarCompressCrypt> logger, EncryptionCommands encryptionCommands, CompressionCommands compressionCommands, IServiceProvider serviceProvider, DatabaseHelper databaseHelper)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _blockListener = blockListener;
            _logger = logger;
            _encryptionCommands = encryptionCommands;
            _compressionCommands = compressionCommands;
            _serviceProvider = serviceProvider;
            _databaseHelper = databaseHelper;
        }

        public async Task<OperationSummary> Compress(CompressOption option)
        {
            var sw = Stopwatch.StartNew();
            var po = ParallelizeOption(option);
            BackupJob job = await _databaseHelper.InitializeBackupJobAsync();

            IEnumerable<CompressionBlock> blocks = option.GenerateCompressBlocks();
            var ordered = PrepareCompressionBlocksAsync(blocks);

            var buffer = new List<CompressionBlock>();
            _logger.LogInformation("Requested order : ");
            await foreach (CompressionBlock block in ordered)
            {
                buffer.Add(block);
                _logger.LogInformation($"{block.BlockName} {block.LastBackupSize.HumanizeSize()}");
            }
            _logger.LogInformation("Starting compression job");

            var operationBlocks = await buffer
                .AsAsyncStream(_cancellationTokenSource.Token)
                .CountAsync(out var counter)
                // Prepare encryption
                .ParallelizeStreamAsync(async (b, token) =>
                {
                    b.StartTime = DateTime.UtcNow;
                    await _encryptionCommands.PrepareEncryptionKey(b, option, token);
                    return b;
                }, po)
                // Core loop 
                .ParallelizeStreamAsync( (block, token) => CompressionBlockInternal(option, block, token), po)
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
            await _databaseHelper.AddBackupBlockJobAsync(operationBlocks, job);
            await _databaseHelper.UpdateBackupJobStatsAsync(sw, job);
            _blockListener.Complete();
            var ops = new OperationSummary(operationBlocks, option.Threads, sw);
            return ops;
        }

        private async Task<OperationCompressionBlock> CompressionBlockInternal(CompressOption option, CompressionBlock block, CancellationToken token)
        {
            int retry = 0;
            CommandResult result = null;
            while (true)
            {
                bool hasError = false;
                try
                {
                    string cmd = _compressionCommands.CompressCommand(block, option);
                    result = await cmd.Run(block.OperationFolder, token);
                    LogReport(block, result);
                    if (result.HasError || result.HasWarning)
                    {
                        hasError = true;
                    }
                }
                catch (Exception e)
                {
                    hasError = true;
                    _logger.LogCritical(e, $"Error compressing {block.Source}");
                }

                if (hasError && Retry.CanRetryIn(out TimeSpan nextRetry, ref retry))
                {
                    _logger.LogWarning($"Retrying compressing {block.Source}, attempt #{retry}");
                    await block.DestinationArchiveFileInfo.TryDeleteFileWithRetryAsync();
                    await Task.Delay(nextRetry);
                }
                else
                {
                    break;
                }
            }
            return new OperationCompressionBlock(block, result);
        }

        public async Task<OperationSummary> Decompress(DecompressOption option)
        {
            var sw = Stopwatch.StartNew();
            var po = ParallelizeOption(option);
            var job = await _databaseHelper.InitializeRestoreJobAsync();

            IEnumerable<DecompressionBatch> blocks = option.GenerateDecompressBlocks();
            IAsyncEnumerable<DecompressionBatch> ordered = PrepareDecompressionBlocksAsync(blocks);

            IReadOnlyCollection<OperationDecompressionsBlock> operationBlocks =
                await ordered
                .AsAsyncStream(_cancellationTokenSource.Token)
                .CountAsync(out var counter)
                // Prepare decryption
                .ParallelizeStreamAsync(async (b, token) =>
                {
                    b.StartTime = DateTime.UtcNow;
                    await _encryptionCommands.PrepareDecryptionKey(b.BackupFull ?? b.BackupsDiff.FirstOrDefault(), option, token);
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

                    return new OperationDecompressionsBlock(blockResults, batch);
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
            await _databaseHelper.AddRestoreBlockJobAsync(job, operationBlocks);
            await _databaseHelper.UpdateRestoreJobStatsAsync(sw, job);
            _blockListener.Complete();
            return new OperationSummary(operationBlocks, option.Threads, sw);
        }

        private async Task<CommandResult> DecompressBlock(DecompressOption option, DecompressionBlock block, CancellationToken token)
        {
            CommandResult result = null;
            try
            {
                string cmd = _compressionCommands.DecompressCommand(block, option);
                result = await cmd.Run(block.OperationFolder, token);

                LogReport(block, result);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error decompressing {block.Source}");
            }
            return result;
        }


        private TccBackupDbContext BackupDb()
        {
            return _serviceProvider.GetRequiredService<TccBackupDbContext>();
        }
        private TccRestoreDbContext RestoreDb()
        {
            return _serviceProvider.GetRequiredService<TccRestoreDbContext>();
        }

        private async IAsyncEnumerable<CompressionBlock> PrepareCompressionBlocksAsync(IEnumerable<CompressionBlock> blocks)
        {
            var db = BackupDb();

            var lastFulls = await db.BackupBlockJobs
                .Include(i => i.BackupSource)
                .Where(j => j.BackupMode == BackupMode.Full)
                .ToListAsync();

            // order by size of bigger backup full to optimize global time
            lastFulls = lastFulls
                .GroupBy(i => i.BackupSource.FullSourcePath)
                .Select(i => i.OrderByDescending(b => b.Size).FirstOrDefault())
                .OrderByDescending(i => i.Size)
                .ToList();

            if (lastFulls.Count == 0)
            {
                // no history ATM, we consider a backup full for each block
                _logger.LogInformation("No backup history, processing files in filesystem order");
                foreach (var b in blocks)
                {
                    b.BackupMode = BackupMode.Full;
                    yield return b;
                }
                yield break;
            }

            await foreach (var b in blocks.OrderBySequence(lastFulls,
                b => b.SourceFileOrDirectory.FullPath,
                p => p.BackupSource.FullSourcePath,
                async (b, p) =>
                {
                    b.LastBackupSize = p.Size;

                    // If already Full here, it's a request from command line
                    if (b.BackupMode.HasValue && b.BackupMode.Value == BackupMode.Full)
                    {
                        return; // we respect command line
                    }

                    var lastBackup = await db.BackupBlockJobs
                        .Where(i => i.BackupSource.FullSourcePath == b.SourceFileOrDirectory.FullPath)
                        .OrderByDescending(i => i.StartTime)
                        .FirstOrDefaultAsync();

                    if (lastBackup != null && string.IsNullOrEmpty(lastBackup.Exception) && b.HaveFullFiles)
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
            IEnumerable<DecompressionBatch> blocks)
        {
            var db = RestoreDb();

            foreach (DecompressionBatch decompBlock in blocks.OrderByDescending(i => i.CompressedSize))
            {
                var opFolder = decompBlock.DestinationFolder;

                // If target directory doesn't exists, then we restore FULL + DIFF
                var dir = new DirectoryInfo(decompBlock.DestinationFolder);
                if (!dir.Exists || !dir.EnumerateDirectories().Any() && !dir.EnumerateFiles().Any())
                {
                    yield return decompBlock;
                    continue;
                }

                var lastRestore = await db.RestoreBlockJobs
                    .Where(i => i.RestoreDestination.FullDestinationPath == opFolder)
                    .OrderByDescending(i => i.StartTime)
                    .FirstOrDefaultAsync();

                if (lastRestore == null)
                {
                    if (decompBlock.BackupFull == null)
                    {
                        throw new Exception("missing backup full");
                    }

                    // no backup full in history : we decompress the FULL + DIFF
                    yield return decompBlock;
                    continue;
                }

                var d = new DecompressionBatch();

                DateTime recent = lastRestore.StartTime;
                // if more recent full, we take it
                if (decompBlock.BackupFull.BackupDate > recent)
                {
                    recent = decompBlock.BackupFull.BackupDate.Value;
                    // if no diff, check more recent full
                    d.BackupFull = decompBlock.BackupFull;
                }

                // we yield all the DIFF archives more recent the the last restore or the last full
                d.BackupsDiff = decompBlock.BackupsDiff?.Where(i => i.BackupDate > recent).ToArray();

                yield return d;
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

        private void LogReport(CompressionBlock block, CommandResult result)
        {
            _logger.LogInformation($"Compressed {block.Source} in {result?.Elapsed.HumanizedTimeSpan()}, {block.DestinationArchiveFileInfo.Length.HumanizeSize()}, {block.DestinationArchiveFileInfo.FullName}");

            if (result?.Infos.Any() ?? false)
            {
                _logger.LogWarning($"Compressed {block.Source} with warning : {string.Join(Environment.NewLine, result.Infos)}");
            }

            if (result?.HasError ?? false)
            {
                _logger.LogError($"Compressed {block.Source} with errors : {result.Errors}");
            }
        }

        private void LogReport(DecompressionBlock block, CommandResult result)
        {
            _logger.LogInformation($"Decompressed {block.Source} in {result?.Elapsed.HumanizedTimeSpan()}, {block.SourceArchiveFileInfo.Length.HumanizeSize()}, {block.SourceArchiveFileInfo.FullName}");

            if (result?.Infos.Any() ?? false)
            {
                _logger.LogWarning($"Decompressed {block.Source} with warning : {string.Join(Environment.NewLine, result.Infos)}");
            }

            if (result?.HasError ?? false)
            {
                _logger.LogError($"Decompressed {block.Source} with errors : {result.Errors}");
            }
        }
    }
}