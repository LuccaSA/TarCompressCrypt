using System.Collections.Concurrent;
using System.Collections.Generic;

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