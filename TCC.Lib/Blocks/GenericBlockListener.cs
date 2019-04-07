using System.Collections.Concurrent;

namespace TCC.Lib.Blocks
{
    public class GenericBlockListener : IBlockListener
    {
        public ConcurrentBag<BlockReport> BlockReports { get; } = new ConcurrentBag<BlockReport>();
        public void OnBlockReport(BlockReport report)
        {
            BlockReports.Add(report);
        }
    }
}