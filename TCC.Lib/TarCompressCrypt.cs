using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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

        public TarCompressCrypt(CancellationTokenSource cancellationTokenSource, IBlockListener blockListener, ILogger<TarCompressCrypt> logger, EncryptionCommands encryptionCommands, CompressionCommands compressionCommands)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _blockListener = blockListener;
            _logger = logger;
            _encryptionCommands = encryptionCommands;
            _compressionCommands = compressionCommands;
        }

        public async Task<OperationSummary> Compress(CompressOption option)
        {
            IEnumerable<Block> blocks = BlockHelper.PreprareCompressBlocks(option);

            var sw = Stopwatch.StartNew();
            var po = new ParallelizeOption
            {
                FailMode = option.FailFast ? Fail.Fast : Fail.Smart,
                MaxDegreeOfParallelism = option.Threads
            };

            var operationBlocks = await blocks
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

            sw.Stop();
            return new OperationSummary(operationBlocks, option.Threads, sw);
        }

        public async Task<OperationSummary> Decompress(DecompressOption option)
        {
            IEnumerable<Block> blocks = BlockHelper.PreprareDecompressBlocks(option);
            var sw = Stopwatch.StartNew();
            var po = new ParallelizeOption
            {
                FailMode = option.FailFast ? Fail.Fast : Fail.Smart,
                MaxDegreeOfParallelism = option.Threads
            };

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
    }
}