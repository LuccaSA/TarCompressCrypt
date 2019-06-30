using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace TCC.Lib.Blocks
{
    public class GenericBlockListener : IBlockListener
    {
        private readonly ConcurrentBag<BlockReport> _blockReports = new ConcurrentBag<BlockReport>();
        private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();
        public Task OnCompressionBlockReportAsync(CompressionBlockReport report)
        {
            _blockReports.Add(report);
            return Task.CompletedTask;
        }

        public Task OnDecompressionBatchReportAsync(DecompressionBlockReport report)
        {
            _blockReports.Add(report);
            return Task.CompletedTask;
        }

        public Task CompletedReports => _tcs.Task;
        public void Complete()
        {
            _tcs.TrySetResult(true);
        }
    }
}