using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TCC.Lib
{
    public class OperationSummary
    {
        private readonly int _threads;
        private OperationStatistic _statistics;
        private double? _compressionRatio;

        public OperationSummary(IEnumerable<IIterationResult> operationBlocks, int threads, Stopwatch stopwatch)
        {
            _threads = threads;
            Stopwatch = stopwatch;
            OperationBlocks = operationBlocks ?? throw new ArgumentNullException(nameof(operationBlocks));
        }

        public IEnumerable<IIterationResult> OperationBlocks { get; }
        public bool IsSuccess => OperationBlocks.All(c => c.IsSuccess);
        public Stopwatch Stopwatch { get; }

        public void ThrowOnError()
        {
            foreach (var result in OperationBlocks)
            {
                result.ThrowOnError();
            }
        }

        public OperationStatistic Statistics
        {
            get
            {
                if (_statistics != null)
                {
                    return _statistics;
                }
                var opStat = new OperationStatistic();
                int count = OperationBlocks.Count();
                double sum = OperationBlocks.Sum(o => o.FileThroughputMbps());
                opStat.Mean = sum / count;
                opStat.Variance = Variance(OperationBlocks, count, opStat.Mean);
                opStat.StandardDeviation = Math.Sqrt(opStat.Variance);
                opStat.StandardError = opStat.StandardError / Math.Sqrt(count);
                opStat.AverageThroughput = opStat.Mean * Math.Min(_threads, count);
                _statistics = opStat;
                return _statistics;
            }
        }

        public TimeSpan MeanTime => TimeSpan.FromMilliseconds(
            OperationBlocks.Sum(o => o.TotalMilliseconds) /
            (double)Math.Min(_threads, OperationBlocks.Count()));

        public long UncompressedSize => OperationBlocks.Sum(o => o.UncompressedSize);

        public long CompressedSize => OperationBlocks.Sum(o => o.CompressedSize);

        public double CompressionRatio
        {
            get
            {
                if (_compressionRatio == null)
                {
                    _compressionRatio = UncompressedSize / (double)CompressedSize;
                }
                return _compressionRatio.Value;
            }
        }

        private static double Variance(IEnumerable<IIterationResult> operations, int count, double mean)
        {
            if (count == 1)
                return 0;
            return operations.Sum(o => (o.FileThroughputMbps() - mean) * (o.FileThroughputMbps() - mean) / (count - 1));
        }
    }
}