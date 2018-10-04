using System.Collections.Generic;

namespace TCC.Lib.Blocks
{
    public interface IBlockListener
    {
        void Add(BlockReport report);
    }

    public class GenericBlockListener : IBlockListener
    {
        public void Add(BlockReport report)
        {
            BlockReports.Add(report);
        }

        public List<BlockReport> BlockReports { get; } = new List<BlockReport>();
    }
}