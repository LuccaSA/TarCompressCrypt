using CommandLine;

namespace TCC.Parser
{
    public class BaseCmdOptions
    {
        [Option('o', "output", Required = true, HelpText = "Output directory path")]
        public string Output { get; set; }

        [Option('t', "threads", HelpText = "Number of threads [1,2...N] / all")]
        public string Threads { get; set; }

        [Option('f', "failFast", HelpText = "Fail-fast mode")]
        public bool FailFast { get; set; }

        [Option('p', "password", HelpText = "encryption password")]
        public string Password { get; set; }

        [Option('e', "passFile", HelpText = "file with password on one line")]
        public string PasswordFile { get; set; }

        [Option('k', "key", HelpText = "Public key for compression, private key for decompression")]
        public string PasswordKey { get; set; }

        [Option('v', "verbose", HelpText = "Verbose output", Default = false)]
        public bool Verbose { get; set; }

        [Option('s', "slackSecret", HelpText = "Slack xoxp Secret")]
        public string SlackSecret { get; set; }
        [Option('c', "slackChannel", HelpText = "Slack #channel name")]
        public string SlackChannel { get; set; }
    }
}