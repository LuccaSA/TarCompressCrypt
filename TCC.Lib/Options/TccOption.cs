namespace TCC.Lib.Options
{
    public class TccOption
    {
        public string SourceDirOrFile { get; set; }

        public string DestinationDir { get; set; }

        public PasswordOption PasswordOption { get; set; } = NoPasswordOption.Nop;

        public int Threads { get; set; }

        public bool FailFast { get; set; }
        public bool Verbose { get; set; }

        public string SlackChannel { get; set; }
        public string SlackSecret { get; set; }
    }
}