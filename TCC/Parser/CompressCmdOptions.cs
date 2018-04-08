using System.Collections.Generic;
using CommandLine;

namespace TCC.Parser
{
    [Verb("compress", HelpText = "Compress specified files/folders")]
    public class CompressCmdOptions : BaseCmdOptions
    {
        [Value(0, Required = true, MetaName = "Source", HelpText = "Files or Folders to compress")]
        public IEnumerable<string> Source { get; set; }

        [Option('i', "individual", HelpText = "Individual mode : create distinct archives for each file / folder in source")]
        public bool Individual { get; set; }
    }
}