using System.Collections.Generic;
using System.Linq;
using TCC.Lib.Blocks;
using TCC.Lib.Database;

namespace TCC.Lib.Options
{
    public class CompressOption : NetworkStorageOptions
    {
        public BlockMode BlockMode { get; set; }
        public CompressionAlgo Algo { get; set; }
        public int CompressionRatio { get; set; }
        public BackupMode? BackupMode { get; set; }
        public int? RetryPeriodInSeconds { get; set; }
        public IEnumerable<string> Filter { get; set; }
        public IEnumerable<string> Exclude { get; set; }
        public bool FolderPerDay { get; set; }
        public int? BoostRatio { get; set; }
        public int? CleanupTime { get; set; }
        public string S3MultipartThreshold { get; set; }
        public string S3MultipartSize { get; set; }
        public IEnumerable<UploadMode> UploadModes { get; set; } = Enumerable.Empty<UploadMode>();
        public UploadMode? UploadMode { get; set; }
    }
}