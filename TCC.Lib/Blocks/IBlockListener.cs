namespace TCC.Lib.Blocks
{
    public interface IBlockListener
    {
        void OnCompressionBlockReport(CompressionBlockReport report);
        void OnDecompressionBatchReport(DecompressionBlockReport report);
    }
}