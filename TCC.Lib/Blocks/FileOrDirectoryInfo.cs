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
                FileInfo = new FileInfo(fileOrDirectory);
                Kind = SourceKind.File;
            }
            else if (Directory.Exists(fileOrDirectory))
            {
                DirectoryInfo = new DirectoryInfo(fileOrDirectory);
                Kind = SourceKind.Directory;
            }
            else
            {
                throw new FileNotFoundException(nameof(fileOrDirectory), fileOrDirectory);
            }
        }

        public FileOrDirectoryInfo(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Kind = SourceKind.File;
        }

        public FileOrDirectoryInfo(DirectoryInfo directoryInfo)
        {
            DirectoryInfo = directoryInfo;
            Kind = SourceKind.Directory;
        }

        public DirectoryInfo DirectoryInfo { get; }
        public FileInfo FileInfo { get; }

        public SourceKind Kind { get; }

        private long? _sourceSize;
        public long SourceSize => (_sourceSize ?? (_sourceSize = FullPath.GetDirectoryOrFileSize())).Value;

        public string FullPath
        {
            get
            {
                switch (Kind)
                {
                    case SourceKind.File:
                        return FileInfo.FullName;
                    case SourceKind.Directory:
                        return DirectoryInfo.FullName;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public string SourceName
        {
            get
            {
                switch (Kind)
                {
                    case SourceKind.File:
                        return FileInfo.Name.Escape();
                    case SourceKind.Directory:
                        return DirectoryInfo.Name.Escape();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public string Name
        {
            get
            {
                switch (Kind)
                {
                    case SourceKind.File:
                        return Path.GetFileNameWithoutExtension(FileInfo.Name);
                    case SourceKind.Directory:
                        return DirectoryInfo.Name;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }

    public enum SourceKind
    {
        File,
        Directory
    }
}