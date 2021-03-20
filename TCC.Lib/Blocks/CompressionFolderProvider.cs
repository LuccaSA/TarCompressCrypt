using System;
using System.IO;
using System.Linq;
using TCC.Lib.Database;
using TCC.Lib.Helpers;

namespace TCC.Lib.Blocks
{
    public class CompressionFolderProvider
    {
        public CompressionFolderProvider(DirectoryInfo destinationRootFolder, bool folderPerDay)
        {
            RootFolder = destinationRootFolder ?? throw new ArgumentNullException(nameof(destinationRootFolder));
            FolderPerDay = folderPerDay;
        }

        public DirectoryInfo RootFolder { get; }
        public bool FolderPerDay { get; }
        private DirectoryInfo _compressDestination;
        
        private DirectoryInfo CompressDestinationRoot
        {
            get
            {
                if (_compressDestination != null)
                {
                    return _compressDestination;
                }
                var root = RootFolder.CreateIfNotExists();
                if (FolderPerDay)
                {
                    var today = DateTime.Today;
                    string subfolder = $"{today.Year:D4}-{today.Month:D2}-{today.Day:D2}";
                    root = root.CreateSubDirectoryIfNotExists(subfolder);
                }
                _compressDestination = root.CreateSubDirectoryIfNotExists(RootFolder.Hostname());
                return _compressDestination;
            }
        }

        public DirectoryInfo GetDirectory(BackupMode? backupMode, string directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName)) throw new ArgumentNullException(nameof(directoryName));
            var archiveRoot = CompressDestinationRoot.CreateSubDirectoryIfNotExists(directoryName);
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

        public bool FullExists(string directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName)) 
                throw new ArgumentNullException(nameof(directoryName));
            var archiveRoot = CompressDestinationRoot.EnumerateDirectories(directoryName).FirstOrDefault();
            if (archiveRoot == null)
            {
                return false;
            }
            var fullFolder = archiveRoot.EnumerateDirectories(TccConst.Full).FirstOrDefault();
            if (fullFolder == null || !fullFolder.Exists)
            {
                return false;
            }
            return fullFolder.GetFiles().Length != 0;
        }
    }
}