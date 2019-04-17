using System;
using TCC.Lib.Blocks;
using TCC.Lib.Command;

namespace TCC.Lib
{
    public class OperationBlock 
    {
        public OperationBlock(Block block, CommandResult commandResult)
        {
            Block = block ?? throw new ArgumentNullException(nameof(block));
            CommandResult = commandResult ?? throw new ArgumentNullException(nameof(commandResult));
        }

        public Block Block { get; }
        public CommandResult CommandResult { get; }

        public double BlockThroughputMbps()
        {
            if (Block.CompressedSize == 0 || CommandResult.ElapsedMilliseconds == 0)
            {
                throw new TccException("Block operation not finished yet");
            }
            if (!CommandResult.IsSuccess)
            {
                return 0;
            }
            return (double)Block.CompressedSize * 1000 / CommandResult.ElapsedMilliseconds;
        } 
    }

    public class OperationCompressionBlock : OperationBlock
    {
        public CompressionBlock CompressionBlock { get; }

        public OperationCompressionBlock(CompressionBlock block, CommandResult commandResult) 
            : base(block, commandResult)
        {
            CompressionBlock = block;
        }
    }

    public class OperationDecompressionsBlock : OperationBlock
    {
        public DecompressionBlock DecompressionBlock { get; }

        public OperationDecompressionsBlock(DecompressionBlock block, CommandResult commandResult)
            : base(block, commandResult)
        {
            DecompressionBlock = block;
        }
    }

}