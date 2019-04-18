using System;
using System.IO;
using TCC.Lib.Helpers;

namespace TCC.Lib.Blocks
{
    public class FileOrDirectoryInfo
    {
        public FileOrDirectoryInfo(string fileOrDirectory)
        {
            if (File.Exists(fileOrDirectory))
            {
                _fileInfo = new FileInfo(fileOrDirectory);
                _kind = SourceKind.File;
            }
            else if (Directory.Exists(fileOrDirectory))
            {
                _directoryInfo = new DirectoryInfo(fileOrDirectory);
                _kind = SourceKind.Directory;
            }
            else
            {
                throw new FileNotFoundException(nameof(fileOrDirectory), fileOrDirectory);
            }
        }

        public FileOrDirectoryInfo(FileInfo fileInfo)
        {
            _fileInfo = fileInfo;
            _kind = SourceKind.File;
        }

        public FileOrDirectoryInfo(DirectoryInfo directoryInfo)
        {
            _directoryInfo = directoryInfo;
            _kind = SourceKind.Directory;
        }

        private DirectoryInfo _directoryInfo;
        private FileInfo _fileInfo;
        private SourceKind _kind;

        enum SourceKind
        {
            File,
            Directory
        }

        private long? _sourceSize;
        public long SourceSize => (_sourceSize ?? (_sourceSize = FullPath.GetDirectoryOrFileSize())).Value;

        public string FullPath
        {
            get
            {
                switch (_kind)
                {
                    case SourceKind.File:
                        return _fileInfo.FullName;
                    case SourceKind.Directory:
                        return _directoryInfo.FullName;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public string SourceName
        {
            get
            {
                switch (_kind)
                {
                    case SourceKind.File:
                        return _fileInfo.Name.Escape();
                    case SourceKind.Directory:
                        return _directoryInfo.Name.Escape();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public string Name
        {
            get
            {
                switch (_kind)
                {
                    case SourceKind.File:
                        return Path.GetFileNameWithoutExtension(_fileInfo.Name);
                    case SourceKind.Directory:
                        return _directoryInfo.Name;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}