namespace TCC.Lib.Options
{
    public class TccOption
    {
        public string SourceDirOrFile { get; set; }

        public string DestinationDir { get; set; }

        public PasswordOption PasswordOption { get; set; } = NoPasswordOption.Nop;

        public int Threads { get; set; }

        public bool FailFast { get; set; }
    }

    public class BenchmarkOption
    {
        public string Source { get; set; }
        public BenchmarkCompressionAlgo Algorithm { get; set; }
        public int Ratio { get; set; }
        public bool? Encrypt { get; set; }
        public BenchmarkContent Content { get; set; }
        public int NumberOfFiles { get; set; }
        public int FileSize { get; set; }
        public int Threads { get; set; }
    }
}