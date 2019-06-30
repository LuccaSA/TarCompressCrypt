using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TCC.Lib.Command;
using TCC.Lib.Database;
using TCC.Lib.Helpers;

namespace TCC.Lib.Blocks
{
    public class DecompressionBlock : Block
    {
        public string OperationFolder { get; set; }
        public FileInfo SourceArchiveFileInfo { get; set; }
        public string Source => SourceArchiveFileInfo.FullName.Escape();

        public override string BlockName => SourceArchiveFileInfo.Name.ExtractArchiveNameAndDate().Name;
        public string BlockDate => SourceArchiveFileInfo.Name.ExtractArchiveNameAndDate().Date.ToString();

        public override FileInfo Archive => SourceArchiveFileInfo;

        public override long UncompressedSize => throw new NotImplementedException();

        public DateTime? BackupDate => SourceArchiveFileInfo.TryExtractBackupDateTime();
    }


    public class DecompressionBatch
    {
        public DecompressionBlock BackupFull { get; set; }
        public DecompressionBlock[] BackupsDiff { get; set; }

        public CommandResult BackupFullCommandResult { get; set; }
        public CommandResult[] BackupDiffCommandResult { get; set; }

        public long CompressedSize
        {
            get
            {
                long sum = 0;
                if (BackupFull != null)
                {
                    sum = BackupFull.Archive.Length;
                }
                if (BackupsDiff != null)
                {
                    sum += BackupsDiff.Sum(b => b.Archive.Length);
                }
                return sum;
            }
        }

        public DateTime StartTime { get; set; }

        public string DestinationFolder
        {
            get
            {
                if (BackupFull != null)
                {
                    return BackupFull.OperationFolder;
                }
                return BackupsDiff?.Select(i => i.OperationFolder).FirstOrDefault();
            }
        }
    }


}