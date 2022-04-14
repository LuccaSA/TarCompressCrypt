using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.AsyncStreams;
using TCC.Lib.Blocks;
using TCC.Lib.Command;
using TCC.Lib.Database;
using TCC.Lib.Dependencies;
using TCC.Lib.Helpers;
using TCC.Lib.Options;
using TCC.Lib.PrepareBlocks;
using TCC.Lib.Storage;

namespace TCC.Lib
{
    public class TarCompressCrypt
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ILogger<TarCompressCrypt> _logger;
        private readonly EncryptionCommands _encryptionCommands;
        private readonly CompressionCommands _compressionCommands;
        private readonly IServiceProvider _serviceProvider;
        private readonly DatabaseHelper _databaseHelper;
        private int _compressCounter;
        private int _uploadCounter;
        private int _totalCounter;

        public TarCompressCrypt(CancellationTokenSource cancellationTokenSource, ILogger<TarCompressCrypt> logger, EncryptionCommands encryptionCommands, CompressionCommands compressionCommands, IServiceProvider serviceProvider, DatabaseHelper databaseHelper)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _logger = logger;
            _encryptionCommands = encryptionCommands;
            _compressionCommands = compressionCommands;
            _serviceProvider = serviceProvider;
            _databaseHelper = databaseHelper;
        }

        public async Task<OperationSummary> Compress(CompressOption option)
        {
            var sw = Stopwatch.StartNew();

            var compFolder = new CompressionFolderProvider(new DirectoryInfo(option.DestinationDir), option.FolderPerDay);

            IEnumerable<CompressionBlock> blocks = option.GenerateCompressBlocks(compFolder);
            IPrepareCompressBlocks prepare = new FileSystemPrepareCompressBlocks(compFolder, option.BackupMode, option.CleanupTime);
            var buffer = prepare.PrepareCompressionBlocksAsync(blocks);
            _totalCounter = buffer.Count;
            PrepareBoostRatio(option, buffer);
            _logger.LogInformation("job prepared in " + sw.Elapsed.HumanizedTimeSpan());
            _logger.LogInformation("Starting compression job");
            var po = ParallelizeOption(option);

            IRemoteStorage uploader = await option.GetRemoteStorageAsync(_logger, _cancellationTokenSource.Token);

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
                .ParallelizeStreamAsync((block, token) =>
                {
                    return CompressionBlockInternal(option, block, token);
                }, po)
                // Cleanup loop
                .ParallelizeStreamAsync(async (opb, token) =>
                {
                    await CleanupOldFiles(opb);
                    await _encryptionCommands.CleanupKey(opb.BlockResult.Block, option, Mode.Compress);
                    return opb;
                }, po)
                // Upload loop
                .ParallelizeStreamAsync((block, token) => UploadBlockInternal(uploader, option, block, token), new ParallelizeOption { FailMode = Fail.Smart, MaxDegreeOfParallelism = option.AzThread ?? 1 })
                .AsReadOnlyCollectionAsync();

            sw.Stop();
            var ops = new OperationSummary(operationBlocks, option.Threads, sw);
            return ops;
        }

        private async Task<OperationCompressionBlock> UploadBlockInternal(IRemoteStorage uploader, CompressOption option, OperationCompressionBlock block, CancellationToken token)
        {
            if (uploader is NoneRemoteStorage)
            {
                return block;
            }

            int count = Interlocked.Increment(ref _uploadCounter);
            string progress = $"{count}/{_totalCounter}";

            var file = block.CompressionBlock.DestinationArchiveFileInfo;
            var name = file.Name;
            RetryContext ctx = null;
            while (true)
            {
                bool hasError;
                try
                {
                    var sw = Stopwatch.StartNew();

                    var result = await uploader.UploadAsync(file, block.CompressionBlock.FolderProvider.RootFolder, token);
                    hasError = !result.IsSuccess;

                    sw.Stop();
                    double speed = file.Length / sw.Elapsed.TotalSeconds;

                    block.BlockResult.StepResults.Add(new StepResult
                    {
                        Type = StepType.Upload,
                        Errors = result.IsSuccess ? null : result.ErrorMessage,
                        Infos = result.IsSuccess ? result.ErrorMessage : null,
                        Duration = sw.Elapsed,
                        ArchiveFileSize = file.Length,
                    });


                    if (!hasError)
                    {
                        _logger.LogInformation($"{progress} Uploaded \"{file.Name}\" in {sw.Elapsed.HumanizedTimeSpan()} at {speed.HumanizedBandwidth()} ");
                    }
                    else
                    {
                        if (ctx == null && option.RetryPeriodInSeconds.HasValue)
                        {
                            ctx = new RetryContext(option.RetryPeriodInSeconds.Value);
                        }
                        _logger.LogError($"{progress} Uploaded {file.Name} with errors. {result.ErrorMessage}");
                    }
                }
                catch (Exception e)
                {
                    hasError = true;
                    if (ctx == null && option.RetryPeriodInSeconds.HasValue)
                    {
                        ctx = new RetryContext(option.RetryPeriodInSeconds.Value);
                    }
                    _logger.LogCritical(e, $"{progress} Error uploading {name}");
                }

                if (hasError)
                {
                    if (ctx != null && await ctx.WaitForNextRetry())
                    {
                        _logger.LogWarning($"{progress} Retrying uploading {name}, attempt #{ctx.Retries}");
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            return block;
        }

        private async Task CleanupOldFiles(OperationCompressionBlock opb)
        {
            if (opb.CompressionBlock.FullsToDelete != null)
            {
                foreach (var full in opb.CompressionBlock.FullsToDelete)
                {
                    try
                    {
                        await full.TryDeleteFileWithRetryAsync();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error while deleting FULL file ");
                    }
                }
            }
            if (opb.CompressionBlock.DiffsToDelete != null)
            {
                foreach (var diff in opb.CompressionBlock.DiffsToDelete)
                {
                    try
                    {
                        await diff.TryDeleteFileWithRetryAsync();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error while deleting DIFF file ");
                    }
                }
            }
        }

        private void PrepareBoostRatio(CompressOption option, IEnumerable<CompressionBlock> buffer)
        {
            _logger.LogInformation("Requested order : ");
            int countFull = 0;
            int countDiff = 0;
            foreach (CompressionBlock block in buffer)
            {
                if (block.BackupMode == BackupMode.Full)
                {
                    countFull++;
                }
                else
                {
                    countDiff++;
                }
            }
            if (option.Threads > 1 && option.BoostRatio.HasValue)
            {
                if (countFull == 0 && countDiff > 0)
                {
                    _logger.LogInformation($"100% diff ({countDiff}), running with X{option.BoostRatio.Value} more threads");
                    // boost mode when 100% of diff, we want to saturate iops : X mode
                    option.Threads = Math.Min(option.Threads * option.BoostRatio.Value, countDiff);
                    if (option.AzThread.HasValue)
                    {
                        option.AzThread = Math.Min(option.AzThread.Value * option.BoostRatio.Value, countDiff);
                    }
                }
                else if (countFull != 0 && (countDiff / (double)(countFull + countDiff) >= 0.9))
                {
                    _logger.LogInformation($"{countFull} full, {countDiff} diffs, running with X{option.BoostRatio.Value} more threads");
                    // boost mode when 95% of diff, we want to saturate iops
                    option.Threads = Math.Min(option.Threads * option.BoostRatio.Value, countDiff);
                    if (option.AzThread.HasValue)
                    {
                        option.AzThread = Math.Min(option.AzThread.Value * option.BoostRatio.Value, countDiff);
                    }
                }
                else
                {
                    _logger.LogInformation($"No boost mode : {countFull} full, {countDiff} diffs");
                }
            }
        }

        private async Task<OperationCompressionBlock> CompressionBlockInternal(CompressOption option, CompressionBlock block, CancellationToken token)
        {

            int count = Interlocked.Increment(ref _compressCounter);
            string progress = $"{count}/{_totalCounter}";
            RetryContext ctx = null;
            CommandResult result = null;
            while (true)
            {
                bool hasError = false;
                try
                {
                    string cmd = _compressionCommands.CompressCommand(block, option);
                    result = await cmd.Run(block.OperationFolder, token);
                    result.ArchiveFileSize = block.DestinationArchiveFileInfo.Length;
                    LogCompressionReport(block, result);

                    if (result.HasError)
                    {
                        hasError = true;
                        if (ctx == null && option.RetryPeriodInSeconds.HasValue)
                        {
                            ctx = new RetryContext(option.RetryPeriodInSeconds.Value);
                        }
                    }
                    var report = $"{progress} [{block.BackupMode}] : {block.BlockName}";
                    if (block.BackupMode == BackupMode.Diff)
                    {
                        report += $" (from {block.DiffDate})";
                    }
                    _logger.LogInformation(report);

                }
                catch (Exception e)
                {
                    hasError = true;
                    if (ctx == null && option.RetryPeriodInSeconds.HasValue)
                    {
                        ctx = new RetryContext(option.RetryPeriodInSeconds.Value);
                    }
                    _logger.LogCritical(e, $"{progress} Error compressing {block.Source}");
                }

                if (hasError)
                {
                    if (ctx != null && await ctx.WaitForNextRetry())
                    {
                        _logger.LogWarning($"{progress} Retrying compressing {block.Source}, attempt #{ctx.Retries}");
                        await block.DestinationArchiveFileInfo.TryDeleteFileWithRetryAsync();
                    }
                    else
                    {
                        await block.DestinationArchiveFileInfo.TryDeleteFileWithRetryAsync();
                        break;
                    }
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

            IEnumerable<DecompressionBatch> blocks = option.GenerateDecompressBlocks().ToList();
            var prepare = new DatabasePreparedDecompressionBlocks(_serviceProvider.GetRequiredService<TccRestoreDbContext>(), _logger);
            IAsyncEnumerable<DecompressionBatch> ordered = prepare.PrepareDecompressionBlocksAsync(blocks);

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
                    int count = Interlocked.Increment(ref _compressCounter);
                    string progress = $"{count}/{_totalCounter}";

                    var blockResults = new List<BlockResult>();

                    if (batch.BackupFull != null)
                    {
                        batch.BackupFullCommandResult = await DecompressBlock(option, batch.BackupFull, token);
                        blockResults.Add(new BlockResult(batch.BackupFull, batch.BackupFullCommandResult, StepType.Decompression));
                    }

                    if (batch.BackupsDiff != null)
                    {
                        batch.BackupDiffCommandResult = new CommandResult[batch.BackupsDiff.Length];
                        for (int i = 0; i < batch.BackupsDiff.Length; i++)
                        {
                            batch.BackupDiffCommandResult[i] = await DecompressBlock(option, batch.BackupsDiff[i], token);
                            blockResults.Add(new BlockResult(batch.BackupsDiff[i], batch.BackupDiffCommandResult[i], StepType.Decompression));
                        }
                    }

                    if (batch.BackupFull != null)
                    {
                        var report = $"{progress} [{BackupMode.Full}] : {batch.BackupFull.BlockName} (from {batch.BackupFull.BlockDate})";
                        _logger.LogInformation(report);
                    }
                    if (batch.BackupsDiff != null)
                    {
                        foreach (var dec in batch.BackupsDiff)
                        {
                            var report = $"{progress} [{BackupMode.Diff}] : {dec.BlockName} (from {dec.BlockDate})";
                            _logger.LogInformation(report);
                        }
                    }

                    return new OperationDecompressionsBlock(blockResults, batch);
                }, po)
                // Cleanup loop
                .ParallelizeStreamAsync(async (odb, token) =>
                {
                    foreach (var b in odb.BlockResults)
                    {
                        await _encryptionCommands.CleanupKey(b.Block, option, Mode.Compress);
                    }
                    return odb;
                }, po)
                .AsReadOnlyCollectionAsync();

            sw.Stop();
            await _databaseHelper.AddRestoreBlockJobAsync(job, operationBlocks);
            await _databaseHelper.UpdateRestoreJobStatsAsync(sw, job);
            return new OperationSummary(operationBlocks, option.Threads, sw);
        }

        private async Task<CommandResult> DecompressBlock(DecompressOption option, DecompressionBlock block, CancellationToken token)
        {
            CommandResult result = null;
            try
            {
                string cmd = _compressionCommands.DecompressCommand(block, option);
                result = await cmd.Run(block.OperationFolder, token);
                result.ArchiveFileSize = block.SourceArchiveFileInfo.Length;

                LogReport(block, result);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error decompressing {block.Source}");
            }
            return result;
        }

        private TccRestoreDbContext RestoreDb()
        {
            return _serviceProvider.GetRequiredService<TccRestoreDbContext>();
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

        private void LogCompressionReport(CompressionBlock block, CommandResult result)
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