using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TCC.Lib.Blocks;
using TCC.Lib.Command;
using TCC.Lib.Helpers;

namespace TCC.Lib
{
    public class OperationBlock
    {
        public OperationBlock(IEnumerable<BlockResult> blockResults)
        {
            BlockResults = blockResults ?? throw new ArgumentNullException(nameof(blockResults));
        }
        
        public IEnumerable<BlockResult> BlockResults { get; }

        public double BlockThroughputMbps()
        {
            if (Size == 0 || ElapsedMilliseconds == 0)
            {
                throw new TccException("Block operation not finished yet");
            }
            if (BlockResults.Any(p => !p.CommandResult.IsSuccess))
            {
                return 0;
            }
            return (double)Size * 1000 / ElapsedMilliseconds;
        }

        private long? _size;
        public long Size
        {
            get => _size ??= BlockResults.Sum(b => b.Block.CompressedSize);
        }

        private long? _elapsedMilliseconds;
        public long ElapsedMilliseconds
        {
            get => _elapsedMilliseconds ??= BlockResults.Sum(b => b.CommandResult.ElapsedMilliseconds);
        }
    }

    public class BlockResult
    {
        public BlockResult(Block block, CommandResult commandResult)
        {
            Block = block;
            CommandResult = new StepResult()
            {
                StepName = "command",
                Duration = commandResult.Elapsed,
                Warning = string.Join(Environment.NewLine, commandResult.Infos),
                Errors = commandResult.Errors
            };
        }

        public Block Block { get; }
        public StepResult CommandResult { get; }
    }
    
    public class StepResult
    {
        public string StepName { get; set; }
        public TimeSpan Duration { get; set; }
        public long ElapsedMilliseconds => (long)Duration.TotalMilliseconds;
        public string Errors { get; set; }
        public string Warning { get; set; }
        public string Infos { get; set; }
        public bool IsSuccess => Errors != null && Warning != null;
        public bool HasError => Errors != null;
        public bool HasWarning => Warning != null;
        public void ThrowOnError()
        {
            if (HasError)
            {
                var sb = new StringBuilder();
                sb.AppendLine("step : " + StepName);
                sb.AppendLine("error : " + Errors);
                throw new TccException(sb.ToString());
            }
        }
    }

    public class OperationCompressionBlock : OperationBlock
    {
        public CompressionBlock CompressionBlock { get; }

        public OperationCompressionBlock(CompressionBlock compressionBlock, CommandResult commandResult)
            : base(new BlockResult(compressionBlock,commandResult).Yield())
        {
            CompressionBlock = compressionBlock;
        }
    }

    public class OperationDecompressionsBlock : OperationBlock
    {
        public DecompressionBatch Batch { get; }
        public IEnumerable<DecompressionBlock> DecompressionBlock { get; }

        public OperationDecompressionsBlock(IEnumerable<BlockResult> blockResults, DecompressionBatch batch)
            : base(blockResults)
        {
            if (blockResults == null) throw new ArgumentNullException(nameof(blockResults));
            Batch = batch;
            DecompressionBlock = blockResults.Select(i => i.Block as DecompressionBlock);
        }
    }

}