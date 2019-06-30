using System.Threading.Tasks;

namespace TCC.Lib.Blocks
{
    public interface IBlockListener
    {
        Task OnCompressionBlockReportAsync(CompressionBlockReport report);
        Task OnDecompressionBatchReportAsync(DecompressionBlockReport report);
        Task CompletedReports { get; }
        void Complete();
    }
}