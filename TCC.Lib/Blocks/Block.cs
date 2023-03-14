using System.IO;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Blocks
{
    public abstract class Block
    {
        public abstract string BlockName { get; }
        public DirectoryInfo ArchiveFolder => Archive.Directory;
        public abstract FileInfo Archive { get; }

        private long _compressedSize;
        public virtual long CompressedSize
        {
            get
            {
                if (_compressedSize == 0)
                {
                    _compressedSize = Archive.FullName.GetDirectoryOrFileSize();
                }
                return _compressedSize;
            }
        }

        public abstract long UncompressedSize { get; }
         
       
        public string BlockPasswordFile { get; set; }
        public CompressionAlgo Algo { get; set; }
        public EncryptionKey EncryptionKey { get; set; }
    }
}