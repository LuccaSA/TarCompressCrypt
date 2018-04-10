using TCC.Lib.Blocks;

namespace TCC.Lib.Options
{
    public class CompressOption : TccOption
    {
        public BlockMode BlockMode { get; set; }
        public CompressionAlgo Algo { get; set; }
        public int CompressionRatio { get; set; }
    }
}