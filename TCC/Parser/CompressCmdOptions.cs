using System.Collections.Generic;
using CommandLine;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Parser
{
    [Verb("compress", HelpText = "Compress specified files/folders")]
    public class CompressCmdOptions : BaseCmdOptions
    {
        [Value(0, Required = true, MetaName = "Source", HelpText = "Files or Folders to compress")]
        public IEnumerable<string> Source { get; set; }

        [Option('i', "individual", HelpText = "Individual mode : create distinct archives for each file / folder in source")]
        public bool Individual { get; set; }

        [Option('a', "algorithm", HelpText = "Algorithm : Lz4 (default), Brotli, Zstd", Default = CompressionAlgo.Lz4)]
        public CompressionAlgo Algorithm { get; set; }

        [Option('r', "ratio", HelpText = "Compression ratio. Valid values are : lz4 [1,9], brotli [1,11], zstd [1,19] ")]
        public int Ratio { get; set; }

        [Option('m', "mode", HelpText = "Backup mode : Full or Diff. (Default = Diff)\n" +
                                        "                      Full : force a full backup for all sources.\n" +
                                        "                      Diff : Archive delta since last full")]
        public BackupMode BackupMode { get; set; }
    }
}