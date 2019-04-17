using TCC.Lib.Command;

namespace TCC.Lib.Blocks
{
    public class CompressionBlockReport : BlockReport
    {
        public CompressionBlockReport(CommandResult result, CompressionBlock block, int blocksCount)
            : base(result, blocksCount, block)
        {
            CompressionBlock = block;
        }

        public CompressionBlock CompressionBlock { get; }
    }
}