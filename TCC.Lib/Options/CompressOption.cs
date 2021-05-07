using System.Collections.Generic;
using TCC.Lib.Blocks;
using TCC.Lib.Database;

namespace TCC.Lib.Options
{
    public class CompressOption : TccOption
    {
        public BlockMode BlockMode { get; set; }
        public CompressionAlgo Algo { get; set; }
        public int CompressionRatio { get; set; }
        public BackupMode? BackupMode { get; set; }
        public int? Retry { get; set; }
        public IEnumerable<string> Filter { get; set; }
        public IEnumerable<string> Exclude { get; set; }
        public bool FolderPerDay { get; set; }
        public int? BoostRatio { get; set; }
        public int? CleanupTime { get; set; }
        public string AzBlobUrl { get; set; }
        public string AzBlobContainer { get; set; }
        public string AzSaS { get; set; }
        public int? AzMbps { get; set; }
        public int? AzThread { get; set; }
        public UploadMode? UploadMode { get; set; }
    }
}