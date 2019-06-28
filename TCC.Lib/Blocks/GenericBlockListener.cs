using System.Collections.Concurrent;

namespace TCC.Lib.Blocks
{
    public class GenericBlockListener : IBlockListener
    {
        public ConcurrentBag<BlockReport> BlockReports { get; } = new ConcurrentBag<BlockReport>();
 
        public void OnCompressionBlockReport(CompressionBlockReport report)
        {
            BlockReports.Add(report);
        }

        public void OnDecompressionBatchReport(DecompressionBlockReport report)
        {
            BlockReports.Add(report);
        }
    }
}