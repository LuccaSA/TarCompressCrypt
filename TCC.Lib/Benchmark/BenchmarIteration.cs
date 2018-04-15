using TCC.Lib.Options;

namespace TCC.Lib.Benchmark
{
    public class BenchmarIteration
    {
        public CompressionAlgo Algo { get; set; }
        public bool Encryption { get; set; }
        public int CompressionRatio { get; set; } 
    }
}