using TCC.Lib.Options;

namespace TCC.Parser
{
    public class TccCommand
    {
        public Mode Mode { get; set; }
        public TccOption Option { get; set; }
        public BenchmarkOption BenchmarkOption { get; set; }
        public int ReturnCode { get; set; }
    }
}