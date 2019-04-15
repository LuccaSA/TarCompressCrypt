using System.IO;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Blocks
{
    public class Block
    {
        private string _source;
        private string _destinationArchive;

        public string Source
        {
            get => _source.Escape();
            set => _source = value;
        }

        public string DestinationArchive
        {
            get => _destinationArchive.Escape();
            set => _destinationArchive = value;
        }
        public string OperationFolder { get; set; }
        public string DestinationFolder { get; set; }
        public string ArchiveName { get; set; }
        public string BlockPasswordFile { get; set; }
        public CompressionAlgo Algo { get; set; }
        public EncryptionKey EncryptionKey { get; set; }

        private long _sourceSize;
        public long SourceSize
        {
            get
            {
                if (_sourceSize == 0)
                {
                    _sourceSize = Path.Combine(OperationFolder, _source).GetDirectoryOrFileSize();
                }
                return _sourceSize;
            }
        }
    }

    public class EncryptionKey
    {
        public EncryptionKey(string key, string keyCrypted)
        {
            Key = key;
            KeyCrypted = keyCrypted;
        }

        public string Key { get; }
        public string KeyCrypted { get; }
    }
}