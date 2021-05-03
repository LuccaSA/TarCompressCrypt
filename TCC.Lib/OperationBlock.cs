using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TCC.Lib.Blocks;
using TCC.Lib.Command;
using TCC.Lib.Helpers;

namespace TCC.Lib
{
    public interface IIterationResult
    {
        double FileThroughputMbps();
        void ThrowOnError();
        long TotalMilliseconds { get; }
        long UncompressedSize { get; }
        long CompressedSize { get; }
        bool IsSuccess { get; }
        bool HasWarning { get; }
        bool HasError { get; }

        IEnumerable<StepResult> StepResults { get; }
    }

    public class OperationCompressionBlock : IIterationResult
    {
        public CompressionBlock CompressionBlock { get; }
        public BlockResult BlockResult { get; }

        public OperationCompressionBlock(CompressionBlock compressionBlock, CommandResult commandResult)
        {
            CompressionBlock = compressionBlock;
            BlockResult = new BlockResult(compressionBlock, commandResult);
        }

        public double FileThroughputMbps() => BlockResult.Yield().BlockFileThroughputMbps();

        public bool IsSuccess => BlockResult.StepResults.All(s => s.IsSuccess);
        public bool HasWarning => BlockResult.StepResults.Any(i => i.HasWarning);
        public bool HasError => BlockResult.StepResults.Any(i => i.HasError);
        public IEnumerable<StepResult> StepResults => BlockResult.StepResults;

        public void ThrowOnError()
        {
            foreach (var result in BlockResult.StepResults)
            {
                result.ThrowOnError();
            }
        }

        public long TotalMilliseconds
        {
            get
            {
                long duration = 0;

                foreach (var stepResult in BlockResult.StepResults)
                {
                    duration += stepResult.ElapsedMilliseconds;
                }

                return duration;
            }
        }

        public long UncompressedSize
        {
            get
            {
                return BlockResult.Block.UncompressedSize;
            }
        }
        public long CompressedSize
        {
            get
            {
                return BlockResult.Block.CompressedSize;
            }
        }
    }

    public class OperationDecompressionsBlock : IIterationResult
    {
        public DecompressionBatch Batch { get; }

        public IEnumerable<DecompressionBlock> DecompressionBlock { get; }
        public IEnumerable<BlockResult> BlockResults { get; }

        public OperationDecompressionsBlock(IEnumerable<BlockResult> blockResults, DecompressionBatch batch)
        {
            if (blockResults == null) throw new ArgumentNullException(nameof(blockResults));
            Batch = batch;
            DecompressionBlock = blockResults.Select(i => i.Block as DecompressionBlock);
            BlockResults = blockResults;
        }

        public double FileThroughputMbps() => this.BlockResults.BlockFileThroughputMbps();

        public bool IsSuccess
        {
            get
            {
                return BlockResults.All(b => b.StepResults.All(s => s.IsSuccess));
            }
        }

        public bool HasWarning => BlockResults.Any(b => b.StepResults.Any(s => s.HasWarning));
        public bool HasError => BlockResults.Any(b => b.StepResults.Any(s => s.HasError));
        public IEnumerable<StepResult> StepResults => BlockResults.SelectMany(b => b.StepResults);

        public void ThrowOnError()
        {
            foreach (var result in BlockResults.SelectMany(b => b.StepResults))
            {
                result.ThrowOnError();
            }
        }

        public long TotalMilliseconds
        {
            get
            {
                long duration = 0;
                foreach (var result in BlockResults)
                {
                    foreach (var stepResult in result.StepResults)
                    {
                        duration += stepResult.ElapsedMilliseconds;
                    }
                }
                return duration;
            }
        }

        public long UncompressedSize
        {
            get
            {
                long size = 0;
                foreach (var result in BlockResults)
                {
                    size += result.Block.UncompressedSize;
                }
                return size;
            }
        }
        public long CompressedSize
        {
            get
            {
                long size = 0;
                foreach (var result in BlockResults)
                {
                    size += result.Block.CompressedSize;
                }
                return size;
            }
        }
    }


    public static class OperationBlockHelper
    {
        public static double BlockFileThroughputMbps(this IEnumerable<BlockResult> blockResults)
        {
            long size = blockResults.Size();
            long ellapsedMs = blockResults.ElapsedFileOperationMilliseconds();
            if (size == 0 || ellapsedMs == 0)
            {
                throw new TccException("Block operation not finished yet");
            }
            if (blockResults
                .SelectMany(i => i.StepResults)
                .Where(s => s.Type == StepType.Compression || s.Type == StepType.Decompression)
                .Any(s => !s.IsSuccess))
            {
                return 0;
            }
            return (double)size * 1000 / ellapsedMs;
        }

        //private long? _size;
        private static long Size(this IEnumerable<BlockResult> blockResults)
        {
            return blockResults.Sum(b => b.Block.CompressedSize);
        }

        //private long? _elapsedFileOperationMilliseconds;
        private static long ElapsedFileOperationMilliseconds(this IEnumerable<BlockResult> blockResults)
        {
            return blockResults
                .SelectMany(b => b.StepResults)
                .Where(s => s.Type == StepType.Compression || s.Type == StepType.Decompression)
                .Sum(s => s.ElapsedMilliseconds);
        }

        //private long? _elapsedUploadOperationMilliseconds;
        private static long ElapsedUploadOperationMilliseconds(this IEnumerable<BlockResult> blockResults)
        {
            return blockResults
                .SelectMany(b => b.StepResults)
                .Where(s => s.Type == StepType.Upload)
                .Sum(s => s.ElapsedMilliseconds);
        }
    }


    public class BlockResult
    {
        public BlockResult(Block block, CommandResult commandResult)
        {
            Block = block;
            StepResults = new List<StepResult>();
            StepResults.Add(new StepResult()
            {
                Name = block.BlockName,
                Type = StepType.Compression,
                Duration = commandResult.Elapsed,
                Infos = string.Join(Environment.NewLine, commandResult.Infos),
                Errors = commandResult.Errors
            });
        }

        public Block Block { get; }
        public List<StepResult> StepResults { get; }
    }


    public class StepResult
    {
        public StepType Type { get; set; }
        public string Name { get; set; }
        public TimeSpan Duration { get; set; }
        public long ElapsedMilliseconds => (long)Duration.TotalMilliseconds;
        public string Errors { get; set; }
        public string Warning { get; set; }
        public string Infos { get; set; }
        public bool IsSuccess => !HasError && !HasWarning;
        public bool HasError => !string.IsNullOrWhiteSpace(Errors);
        public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);

        public void ThrowOnError()
        {
            if (HasError)
            {
                var sb = new StringBuilder();
                sb.AppendLine("step : " + Type);
                sb.AppendLine("error : " + Errors);
                throw new TccException(sb.ToString());
            }
        }
    }

    public enum StepType
    {
        Compression,
        Decompression,
        Upload
    }


}