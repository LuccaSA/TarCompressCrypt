using TCC.Lib.Command;

namespace TCC.Lib.Blocks
{
    public class BlockReport
    {
        public BlockReport(CommandResult result, Block block, int blocksCount)
        {
            Cmd = result;
            Block = block;
            Total = blocksCount;
        }

        public CommandResult Cmd { get; }
        public Block Block { get; }
        public int Total { get; }
    }
}