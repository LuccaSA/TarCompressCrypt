using System;
using System.IO;
using TCC.Lib.Database;
using TCC.Lib.Helpers;

namespace TCC.Lib.Blocks
{
    public class CompressionFolderProvider
    {
        public CompressionFolderProvider(DirectoryInfo destinationRootFolder)
        {
            _destinationRootFolder = destinationRootFolder ?? throw new ArgumentNullException(nameof(destinationRootFolder));
        }

        private readonly DirectoryInfo _destinationRootFolder;
        private DirectoryInfo _destinationRoot;
        private DirectoryInfo DestinationRoot
        {
            get
            {
                if (_destinationRoot != null)
                {
                    return _destinationRoot;
                }
                var root = _destinationRootFolder.CreateIfNotExists();
                _destinationRoot = root.CreateSubDirectoryIfNotExists(_destinationRootFolder.Hostname());
                return _destinationRoot;
            }
        }

        public DirectoryInfo GetDirectory(BackupMode? backupMode, string directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName)) throw new ArgumentNullException(nameof(directoryName));
            var archiveRoot = DestinationRoot.CreateSubDirectoryIfNotExists(directoryName);
            switch (backupMode)
            {
                case BackupMode.Diff:
                    return archiveRoot.CreateSubDirectoryIfNotExists(TccConst.Diff);
                case BackupMode.Full:
                    return archiveRoot.CreateSubDirectoryIfNotExists(TccConst.Full);
                case null:
                    return archiveRoot;
                default:
                    throw new ArgumentOutOfRangeException(nameof(backupMode), backupMode, null);
            }
        }
    }
}