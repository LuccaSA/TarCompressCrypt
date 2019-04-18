using System;
using System.IO;
using TCC.Lib.Database;
using TCC.Lib.Helpers;

namespace TCC.Lib.Blocks
{
    public class CompressionFolderProvider
    {

        private DirectoryInfo _diffFolder;
        private DirectoryInfo _fullFolder;
        private readonly object _sync = new object();

        public CompressionFolderProvider(DirectoryInfo destinationRootFolder)
        {
            DestinationRootFolder = destinationRootFolder ?? throw new ArgumentNullException(nameof(destinationRootFolder));
        }

        public DirectoryInfo DestinationRootFolder { get; }

        private DirectoryInfo DiffFolder
        {
            get
            {
                if (_diffFolder != null)
                {
                    return _diffFolder;
                }
                lock (_sync)
                {
                    DestinationRootFolder.CreateIfNotExists();
                    _diffFolder = DestinationRootFolder.CreateSubDirectoryIfNotExists(TccConst.Diff);
                }
                return _diffFolder;
            }
        }

        private DirectoryInfo FullFolder
        {
            get
            {
                if (_fullFolder != null)
                {
                    return _fullFolder;
                }
                lock (_sync)
                {
                    DestinationRootFolder.CreateIfNotExists();
                    _fullFolder = DestinationRootFolder.CreateSubDirectoryIfNotExists(TccConst.Full);
                }
                return _fullFolder;
            }
        }

        public DirectoryInfo GetDirectory(BackupMode backupMode)
        {
            switch (backupMode)
            {
                case BackupMode.Diff:
                    return DiffFolder;
                case BackupMode.Full:
                    return FullFolder;
                default:
                    throw new ArgumentOutOfRangeException(nameof(backupMode), backupMode, null);
            }
        }
    }
}