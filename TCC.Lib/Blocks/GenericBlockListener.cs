using System.Collections.Generic;

namespace TCC.Lib.Blocks
{
    public class GenericBlockListener : IBlockListener
    {
        public void Add(BlockReport report)
        {
            BlockReports.Add(report);
        }

        public List<BlockReport> BlockReports { get; } = new List<BlockReport>();
    }
}