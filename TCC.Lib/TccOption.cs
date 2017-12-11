namespace TCC
{
    public class CompressOption : TccOption
    {
        public bool Individual { get; set; }
    }

    public class DecompressOption : TccOption
    {
    }

    public class TccOption
    {
        public string SourceDirOrFile { get; set; }

        public string DestinationDir { get; set; }

        public PasswordMode PasswordMode { get; set; }

        public string Password { get; set; }

        public string PasswordFile { get; set; }

        public string PublicPrivateKeyFile { get; set; }

        public string Threads { get; set; }

        public bool FailFast { get; set; }

    }
}