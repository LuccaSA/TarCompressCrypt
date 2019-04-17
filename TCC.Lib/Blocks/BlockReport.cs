using TCC.Lib.Command;

namespace TCC.Lib.Blocks
{
    public class BlockReport
    {
        public BlockReport(CommandResult result, int blocksCount, Block block)
        {
            Cmd = result;
            Total = blocksCount;
            Block = block;
        }

        public CommandResult Cmd { get; }
        public int Total { get; }
        public Block Block { get; }
    }
}