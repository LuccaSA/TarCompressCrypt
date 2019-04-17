using System.IO;
using TCC.Lib.Database;
using TCC.Lib.Helpers;

namespace TCC.Lib.Blocks
{
    public class CompressionBlock : Block
    {
        internal CompressionFolderProvider FolderProvider { get; set; }

        public string DestinationArchiveExtension { get; set; }
        public DirectoryInfo SourceOperationFolder { get; set; }

        public string OperationFolder => SourceOperationFolder.FullName;

        public FileOrDirectoryInfo SourceFileOrDirectory { get; set; }

        public string Source
            => SourceFileOrDirectory.SourceName;

        public string DestinationArchiveName
            => SourceFileOrDirectory.Name;

        public string DestinationArchive
            => DestinationArchiveFileInfo.FullName.Escape();

        public FileInfo DestinationArchiveFileInfo
            => new FileInfo(Path.Combine(DestinationArchiveFolder.FullName, DestinationArchiveName + DestinationArchiveExtension));

        public DirectoryInfo DestinationArchiveFolder
        {
            get
            {
                //return FolderProvider.DestinationRootFolder;
                return FolderProvider.GetDirectory(BackupMode ?? Database.BackupMode.Diff)
                    .CreateSubDirectoryIfNotExists(DestinationArchiveName);
            }
        }

        public BackupMode? BackupMode { get; set; }

        public override string BlockName => DestinationArchiveName;
        public override FileInfo Archive => DestinationArchiveFileInfo;


        public override long UncompressedSize => SourceFileOrDirectory.SourceSize;
    }
}