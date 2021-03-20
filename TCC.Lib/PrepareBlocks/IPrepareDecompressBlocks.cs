using System.Collections.Generic;
using TCC.Lib.Blocks;

namespace TCC.Lib.PrepareBlocks
{
    public interface IPrepareDecompressBlocks
    {
        public IAsyncEnumerable<DecompressionBatch> PrepareDecompressionBlocksAsync(IEnumerable<DecompressionBatch> blocks);
    }
}