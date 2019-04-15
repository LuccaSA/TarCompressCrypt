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

        public TarCompressCrypt(CancellationTokenSource cancellationTokenSource, IBlockListener blockListener, ILogger<TarCompressCrypt> logger, EncryptionCommands encryptionCommands)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _blockListener = blockListener;
            _logger = logger;
            _encryptionCommands = encryptionCommands;
        }

        public Task<OperationSummary> Compress(CompressOption compressOption)
        {
            IEnumerable<Block> blocks = BlockHelper.PreprareCompressBlocks(compressOption);

            return ProcessingLoop(blocks, compressOption, _encryptionCommands.Encrypt);
        }

        public Task<OperationSummary> Decompress(DecompressOption decompressOption)
        {
            IEnumerable<Block> blocks = BlockHelper.PreprareDecompressBlocks(decompressOption);

            return ProcessingLoop(blocks, decompressOption, _encryptionCommands.Decrypt);
        }

        private async Task<OperationSummary> ProcessingLoop(IEnumerable<Block> blocks,
            TccOption option,
            Func<Block, TccOption,CancellationToken, Task<CommandResult>> processor)
        {
            var sw = Stopwatch.StartNew();
            var po = new ParallelizeOption
            {
                FailMode = option.FailFast ? Fail.Fast : Fail.Smart,
                MaxDegreeOfParallelism = option.Threads
            };

            var operationBlocks = await blocks
                    .AsAsyncStream(_cancellationTokenSource.Token)
                    .CountAsync(out var counter)
                    .ParallelizeStreamAsync(async (b, token) =>
                    {
                        _logger.LogInformation($"Starting {b.Source}");
                        CommandResult result = await processor(b, option, token);
                        _logger.LogInformation($"Finished {b.Source} on {result.ElapsedMilliseconds} ms");
                        return result;
                    }, po)
                    .SelectAsync((v, ct) =>
                    {
                        if (v is StreamedValue<CommandResult, Block> value)
                        {
                            if (value.Exception != null)
                            {
                                _logger.LogError(value.Exception, $"Error on {value.ItemSource.Source}");
                                value.Item.Errors += value.Exception.Message; 
                            }
                            return Task.FromResult(new OperationBlock(value.ItemSource, value.Item));
                        }
                        return Task.FromException<OperationBlock>(new Exception("TCC internal error"));
                    })
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