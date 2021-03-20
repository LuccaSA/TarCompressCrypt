using System;
using System.IO;
using TCC.Lib.Database;
using TCC.Lib.Helpers;

namespace TCC.Lib.Blocks
{
    public class CompressionBlock : Block
    {
        private DateTime? _startTime;
        internal CompressionFolderProvider FolderProvider { get; set; }

        public string DestinationArchiveExtension { get; set; }
        public DirectoryInfo SourceOperationFolder { get; set; }

        public string OperationFolder => SourceOperationFolder.FullName;

        public FileOrDirectoryInfo SourceFileOrDirectory { get; set; }

        public string Source => SourceFileOrDirectory.SourceName;

        public string DestinationArchiveName
        {
            get
            {
                switch (BackupMode)
                {
                    case Database.BackupMode.Diff:
                        return $"{SourceFileOrDirectory.Name}_{StartTime:yyyyMMddHHmmss}.diff";
                    case Database.BackupMode.Full:
                        return $"{SourceFileOrDirectory.Name}_{StartTime:yyyyMMddHHmmss}.full";
                   default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public string DestinationArchive
            => DestinationArchiveFileInfo.FullName.Escape();

        public FileInfo DestinationArchiveFileInfo
            => new FileInfo(Path.Combine(DestinationArchiveFolder.FullName, DestinationArchiveName + DestinationArchiveExtension));

        public DirectoryInfo DestinationArchiveFolder =>
            FolderProvider.GetDirectory(BackupMode, SourceFileOrDirectory.Name);

        public bool HaveFullFiles =>
            FolderProvider.FullExists(SourceFileOrDirectory.Name);

        public BackupMode BackupMode { get; set; }

        public override string BlockName => SourceFileOrDirectory.Name;
        public override FileInfo Archive => DestinationArchiveFileInfo;


        public override long UncompressedSize => SourceFileOrDirectory.SourceSize;

        public DateTime? DiffDate { get; set; }

        public DateTime StartTime
        {
            get => _startTime ?? throw new ArgumentOutOfRangeException( nameof(StartTime),"CompressionBlock StartTime undefined");
            set => _startTime = value;
        }

        public long LastBackupSize { get; set; }
    }
}