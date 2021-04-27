using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Threading.Channels;
using TCC.Lib.AsyncStreams;
using TCC.Lib.Blocks;
using TCC.Lib.Command;
using TCC.Lib.Database;
using TCC.Lib.Dependencies;
using TCC.Lib.Helpers;
using TCC.Lib.Options;
using TCC.Lib.PrepareBlocks;

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
        private readonly UploadCommands _uploadCommands;

        public TarCompressCrypt(CancellationTokenSource cancellationTokenSource, IBlockListener blockListener, ILogger<TarCompressCrypt> logger, EncryptionCommands encryptionCommands, CompressionCommands compressionCommands, IServiceProvider serviceProvider, DatabaseHelper databaseHelper, UploadCommands uploadCommands)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _blockListener = blockListener;
            _logger = logger;
            _encryptionCommands = encryptionCommands;
            _compressionCommands = compressionCommands;
            _serviceProvider = serviceProvider;
            _databaseHelper = databaseHelper;
            _uploadCommands = uploadCommands;
        }

        public async Task<OperationSummary> Compress(CompressOption option)
        {
            var sw = Stopwatch.StartNew();

            var compFolder = new CompressionFolderProvider(new DirectoryInfo(option.DestinationDir), option.FolderPerDay);

            IEnumerable<CompressionBlock> blocks = option.GenerateCompressBlocks(compFolder);
            IPrepareCompressBlocks prepare = new FileSystemPrepareCompressBlocks(compFolder, option.BackupMode, option.CleanupTime);
            var buffer = prepare.PrepareCompressionBlocksAsync(blocks);
            PrepareBoostRatio(option, buffer);
            _logger.LogInformation("job prepared in " + sw.Elapsed.HumanizedTimeSpan());
            _logger.LogInformation("Starting compression job");
            var po = ParallelizeOption(option);

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
                .ParallelizeStreamAsync((block, token) => CompressionBlockInternal(option, block, token), po)
                // Cleanup loop
                .ParallelizeStreamAsync(async (opb, token) =>
                {
                    await CleanupOldFiles(opb);
                    await _encryptionCommands.CleanupKey(opb.BlockResults.First().Block, option, opb.BlockResults.First().CommandResult, Mode.Compress);
                    return opb;
                }, po)
                // Upload loop
                .ParallelizeStreamAsync((block, token) => UploadBlockInternal(option, block, token), new ParallelizeOption { FailMode = Fail.Smart, MaxDegreeOfParallelism = option.AzThread ?? 1 })
                // notif
                .ForEachAsync(async (i, ct) =>
                {
                    await _blockListener.OnCompressionBlockReportAsync(new CompressionBlockReport(i.Item.BlockResults.First().CommandResult, i.Item.CompressionBlock, counter.Count));
                })
                .AsReadOnlyCollectionAsync();



            sw.Stop();
            _blockListener.Complete();
            var ops = new OperationSummary(operationBlocks, option.Threads, sw);
            return ops;
        }

        private async Task<OperationCompressionBlock> UploadBlockInternal(CompressOption option, OperationCompressionBlock block, CancellationToken token)
        {
            if (string.IsNullOrEmpty(option.AzBlobUrl)
                || string.IsNullOrEmpty(option.AzBlobContainer)
                || string.IsNullOrEmpty(option.AzSaS)
                || !option.UploadMode.HasValue)
            {
                return block;
            }

            var file = block.CompressionBlock.DestinationArchiveFileInfo;
            var name = file.Name;


            switch (option.UploadMode)
            {
                case UploadMode.AzCopy:
                    try
                    {
                        var cmd = _uploadCommands.UploadCommand(option,
                            file,
                            block.CompressionBlock.FolderProvider.RootFolder);

                        bool success = await AzCopyUploadOnBlobAsync(block.CompressionBlock.FolderProvider.RootFolder.FullName, cmd, token);

                        LogUploadReport(name, file.Length, success);
                    }
                    catch (Exception e)
                    {
                        _logger.LogCritical(e, $"Error uploading {name}");
                    }

                    break;
                case UploadMode.AzureSdk:
                    try
                    {
                        await SdkUploadOnBlobAsync(option, block.CompressionBlock.FolderProvider.RootFolder, file, token);
                    }
                    catch (Exception e)
                    {
                        _logger.LogCritical(e, $"Error uploading {name}");
                    }
                    break;
            }


            return block;
        }

        internal static async Task<bool> SdkUploadOnBlobAsync(CompressOption option, DirectoryInfo rootFolder,
             FileInfo file, CancellationToken token)
        {
            var client = new BlobServiceClient(new Uri(option.AzBlobUrl + "/" + option.AzBlobContainer + "?" + option.AzSaS));
            
            var container = client.GetBlobContainerClient(option.AzBlobContainer);
            string targetPath = file.GetRelativeTargetPathTo(rootFolder);
            using FileStream uploadFileStream = File.OpenRead(file.FullName);
            var response = await container.UploadBlobAsync(targetPath, uploadFileStream, token);
            return response.GetRawResponse().Status == 201;
        }

        internal static async Task<bool> AzCopyUploadOnBlobAsync(string opFolder, string cmd, CancellationToken token)
        {
            var result = await cmd.Run(opFolder, token);

            var infos = result.Output
                .Split(Environment.NewLine)
                .Select(i => JsonSerializer.Deserialize<AzCopyResponse>(i))
                .FirstOrDefault(i => i.MessageType == "EndOfJob");

            if (infos == null)
            {
                Console.WriteLine(result.Output);
                return false;
            }

            var jobResult = JsonSerializer.Deserialize<AzCopyJobCompleted>(infos.MessageContent);

            return jobResult.TransfersCompleted == "1";
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
                }
                else if (countFull != 0 && (countDiff / (double)(countFull + countDiff) >= 0.9))
                {
                    _logger.LogInformation($"{countFull} full, {countDiff} diffs, running with X{option.BoostRatio.Value} more threads");
                    // boost mode when 95% of diff, we want to saturate iops
                    option.Threads = Math.Min(option.Threads * option.BoostRatio.Value, countDiff);
                }
                else
                {
                    _logger.LogInformation($"No boost mode : {countFull} full, {countDiff} diffs");
                }
            }
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
                    LogCompressionReport(block, result);
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

                if (option.Retry.HasValue && hasError &&
                    Retry.CanRetryIn(out TimeSpan nextRetry, ref retry, option.Retry.Value))
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

        private void LogUploadReport(string fileName, long length, bool success)
        {
            _logger.LogInformation($"Uploaded {fileName}");

            if (!success)
            {
                _logger.LogError($"Uploaded {fileName} with errors");
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

    public enum UploadMode
    {
        AzCopy,
        AzureSdk
    }
}