using System;
using System.IO;
using TCC.Lib.Helpers;

namespace TCC.Lib.Blocks
{
    public class DecompressionBlock : Block
    {
        public string OperationFolder { get; set; }
        public FileInfo SourceArchiveFileInfo { get; set; }
        public string Source => SourceArchiveFileInfo.FullName.Escape();

        public override string BlockName => SourceArchiveFileInfo.Name;
        public override FileInfo Archive => SourceArchiveFileInfo;

        public override long UncompressedSize => throw new NotImplementedException();
    }
}