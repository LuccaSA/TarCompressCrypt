using System.Collections.Generic;
using CommandLine;

namespace TCC.Parser
{
    [Verb("decompress", HelpText = "Decompress specified files/folders")]
    public class DecompressOptions : BaseCmdOptions
    {
        [Value(0, Required = true, MetaName = "Source", HelpText = "Files or Folders to decompress")]
        public IEnumerable<string> Source { get; set; }
    }
}