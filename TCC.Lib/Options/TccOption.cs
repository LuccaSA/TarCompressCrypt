namespace TCC.Lib.Options
{
    public class TccOption
    {
        public string SourceDirOrFile { get; set; }

        public string DestinationDir { get; set; }

        public CompressionAlgo Algo { get; set; }

        public PasswordOption PasswordOption { get; set; } = NoPasswordOption.Nop;

        public int Threads { get; set; }

        public bool FailFast { get; set; }
    }
}