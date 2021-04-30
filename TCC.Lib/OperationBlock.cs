using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TCC.Lib.Blocks;
using TCC.Lib.Command;
using TCC.Lib.Helpers;

namespace TCC.Lib
{
    public class OperationBlock
    {
        public OperationBlock(IEnumerable<BlockResult> blockResults)
        {
            BlockResults = blockResults ?? throw new ArgumentNullException(nameof(blockResults));
        }

        public IEnumerable<BlockResult> BlockResults { get; }

        public double BlockFileThroughputMbps()
        {
            if (Size == 0 || ElapsedFileOperationMilliseconds == 0)
            {
                throw new TccException("Block operation not finished yet");
            }
            if (BlockResults
                .SelectMany(i => i.StepResults)
                .Where(s => s.Type == StepType.Compression || s.Type == StepType.Decompression)
                .Any(s => !s.IsSuccess))
            {
                return 0;
            }
            return (double)Size * 1000 / ElapsedFileOperationMilliseconds;
        }

        private long? _size;
        public long Size
        {
            get => _size ??= BlockResults.Sum(b => b.Block.CompressedSize);
        }

        private long? _elapsedFileOperationMilliseconds;
        public long ElapsedFileOperationMilliseconds
        {
            get => _elapsedFileOperationMilliseconds ??= BlockResults
                .SelectMany(b=>b.StepResults)
                .Where(s => s.Type == StepType.Compression || s.Type == StepType.Decompression)
                .Sum(s => s.ElapsedMilliseconds);
        }

        private long? _elapsedUploadOperationMilliseconds;
        public long ElapsedUploadOperationMilliseconds
        {
            get => _elapsedUploadOperationMilliseconds ??= BlockResults
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

    public class OperationCompressionBlock : OperationBlock
    {
        public CompressionBlock CompressionBlock { get; }

        public OperationCompressionBlock(CompressionBlock compressionBlock, CommandResult commandResult)
            : base(new BlockResult(compressionBlock, commandResult).Yield())
        {
            CompressionBlock = compressionBlock;
        }
    }

    public class OperationDecompressionsBlock : OperationBlock
    {
        public DecompressionBatch Batch { get; }
        public IEnumerable<DecompressionBlock> DecompressionBlock { get; }

        public OperationDecompressionsBlock(IEnumerable<BlockResult> blockResults, DecompressionBatch batch)
            : base(blockResults)
        {
            if (blockResults == null) throw new ArgumentNullException(nameof(blockResults));
            Batch = batch;
            DecompressionBlock = blockResults.Select(i => i.Block as DecompressionBlock);
        }
    }

}