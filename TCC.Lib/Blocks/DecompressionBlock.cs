using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TCC.Lib.Database;
using TCC.Lib.Helpers;

namespace TCC.Lib.Blocks
{
    public class DecompressionBlock : Block
    {
        public string OperationFolder { get; set; }
        public FileInfo SourceArchiveFileInfo { get; set; }
        public string Source => SourceArchiveFileInfo.FullName.Escape();

        public override string BlockName
        {
            get
            {
                var name = Path.GetFileNameWithoutExtension(SourceArchiveFileInfo.Name);

                if (name.Length <= 15)
                {
                    return name;
                }

                var sep = name.Length - 15;
                if (name[sep] != '_')
                {
                    return name;
                }

                for (int i = sep + 1; i < name.Length; i++)
                {
                    char c = name[i];
                    if (!char.IsDigit(c))
                    {
                        return name;
                    }
                }

                return name.Substring(0, sep);
            }
        }

        public override FileInfo Archive => SourceArchiveFileInfo;

        public override long UncompressedSize => throw new NotImplementedException();

        public DateTime? BackupDate
        {
            get
            {
                int segment = SourceArchiveFileInfo.Name.LastIndexOf('_');
                if (segment <= 0)
                {
                    return null;
                }
                var info = SourceArchiveFileInfo.Name.Substring(segment);
                if (info.Length < 14)
                {
                    return null;
                }
                var dt = info.Substring(0, 14);
                if (DateTime.TryParseExact(dt, _dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                {
                    return parsed;
                }
                return null;
            }
        }

        private const string _dateFormat = "yyyyMMddHHmmss";
    }


    public class DecompressionBatch
    {
        public DecompressionBlock BackupFull { get; set; }
        public List<DecompressionBlock> BackupsDiff { get; set; }

        public long Size
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
    }


}