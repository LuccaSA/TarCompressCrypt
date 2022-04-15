namespace TCC.Lib.Options
{
    public class TccOption : ISlackOption
    {
        public string SourceDirOrFile { get; set; }
        public string DestinationDir { get; set; }

        public PasswordOption PasswordOption { get; set; } = NoPasswordOption.Nop;
        public int Threads { get; set; }
        public bool FailFast { get; set; }
        public bool IgnoreMissingFull { get; set; }

        public string SlackChannel { get; set; }
        public string SlackSecret { get; set; }
        public string BucketName { get; set; }
        public bool SlackOnlyOnError { get; set; }

        public bool Verbose { get; set; }
        public string LogPaths { get; set; }
        public string AuditFilePath { get; set; }
    }
}