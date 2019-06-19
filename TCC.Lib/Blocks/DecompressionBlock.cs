using System;
using System.IO;
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

        public RestoreMode? RestoreMode { get; set; }

        public override FileInfo Archive => SourceArchiveFileInfo;

        public override long UncompressedSize => throw new NotImplementedException();
    }
}