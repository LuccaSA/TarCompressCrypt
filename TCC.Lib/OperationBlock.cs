using System;
using System.Collections.Generic;
using System.Linq;
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

        //public IEnumerable<Block> Block { get; }

        //public IEnumerable<CommandResult> CommandResult { get; }

        public IEnumerable<BlockResult> BlockResults { get; }

        public double BlockThroughputMbps()
        {
            if (BlockResults.Sum(b => b.Block.CompressedSize) == 0 || BlockResults.Sum(b => b.CommandResult.ElapsedMilliseconds) == 0)
            {
                throw new TccException("Block operation not finished yet");
            }
            if (BlockResults.Any(p => !p.CommandResult.IsSuccess))
            {
                return 0;
            }
            return (double)BlockResults.Sum(b => b.Block.CompressedSize) * 1000 / BlockResults.Sum(b => b.CommandResult.ElapsedMilliseconds);
        }
    }

    public class BlockResult
    {
        public BlockResult(Block block, CommandResult commandResult)
        {
            Block = block;
            CommandResult = commandResult;
        }

        public Block Block { get; }
        public CommandResult CommandResult { get;  }
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