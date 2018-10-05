using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Blocks
{
    public class Block
    {
        private string _source;
        private string _destinationArchive;
        public string OperationFolder { get; set; }

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

        public string DestinationFolder { get; set; }
        public string ArchiveName { get; set; }
        public string BlockPasswordFile { get; set; }
        public CompressionAlgo Algo { get; set; }
    }
}