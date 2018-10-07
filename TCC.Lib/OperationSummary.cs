using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TCC.Lib.Blocks;
using TCC.Lib.Command;

namespace TCC.Lib
{
    public class OperationSummary
    {
        private readonly int _threads;
        private OperationStatistic _statistics;
        private double? _compressionRatio;

        public OperationSummary(IEnumerable<OperationBlock> operationBlocks, int threads, Stopwatch stopwatch)
        {
            _threads = threads;
            Stopwatch = stopwatch;
            OperationBlocks = operationBlocks ?? throw new ArgumentNullException(nameof(operationBlocks));
        }

        public IEnumerable<OperationBlock> OperationBlocks { get; }
        public bool IsSuccess => OperationBlocks.All(c => c.CommandResult.IsSuccess);
        public Stopwatch Stopwatch { get; }

        public void ThrowOnError()
        {
            foreach (var result in OperationBlocks.Select(o => o.CommandResult))
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
                double sum = OperationBlocks.Sum(o => o.BlockThroughputMbps());
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
            OperationBlocks.Sum(o => o.CommandResult.ElapsedMilliseconds) /
            (double) Math.Min(_threads, OperationBlocks.Count()));

        public long UncompressedSize => OperationBlocks.Sum(o => o.Block.SourceSize);
        public long CompressedSize => OperationBlocks.Sum(i => new FileInfo(i.Block.DestinationArchive.Trim('"')).Length);

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

        private static double Variance(IEnumerable<OperationBlock> operations, int count, double mean)
        {
            if (count == 1)
                return 0;
            return operations.Sum(o => (o.BlockThroughputMbps() - mean) * (o.BlockThroughputMbps() - mean) / (count - 1));
        }
    }

    public class OperationStatistic
    {
        /// <summary>
        /// Mean throughput in Mbps per thread
        /// </summary>
        public double Mean { get; set; }
        public double Variance { get; set; }
        public double StandardDeviation { get; set; }
        public double StandardError { get; set; }
        /// <summary>
        /// Average throughput on all threads
        /// </summary>
        public double AverageThroughput { get; set; }
    }

    public class OperationBlock
    {
        public OperationBlock(Block block, CommandResult commandResult)
        {
            Block = block ?? throw new ArgumentNullException(nameof(block));
            CommandResult = commandResult ?? throw new ArgumentNullException(nameof(commandResult));
        }

        public Block Block { get; }
        public CommandResult CommandResult { get; }

        public double BlockThroughputMbps()
        {
            if (Block.SourceSize == 0 || CommandResult.ElapsedMilliseconds == 0)
            {
                throw new TccException("Block operation not finished yet");
            }
            if (!CommandResult.IsSuccess)
            {
                return 0;
            }
            return (double)Block.SourceSize * 1000 / CommandResult.ElapsedMilliseconds;
        } 
    }
}