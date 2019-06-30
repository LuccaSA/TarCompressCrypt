using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TCC.Lib.Helpers
{
    public static class DirectoryInfoExtensions
    {
        public static DirectoryInfo CreateSubDirectoryIfNotExists(this DirectoryInfo directoryInfo, string directoryName)
        {
            var found = directoryInfo.EnumerateDirectories(directoryName).FirstOrDefault();
            return found ?? directoryInfo.CreateSubdirectory(directoryName);
        }

        public static DirectoryInfo CreateIfNotExists(this DirectoryInfo directoryInfo)
        {
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }
            return directoryInfo;
        }

        public static string Hostname(this DirectoryInfo directoryInfo)
        {
            if (!directoryInfo.FullName.StartsWith("\\"))
            {
                return Environment.MachineName;
            }
            string hostPart = directoryInfo.FullName.Substring(2);
            int endIndex = hostPart.IndexOf('\\');
            if (endIndex <= 0)
            {
                throw new ArgumentException("directoryInfo is not a correct network path");
            }
            return hostPart.Substring(0, hostPart.IndexOf('\\'));
        }

        public static DateTime ExtractBackupDateTime(this FileInfo sourceArchiveFileInfo)
        {
            return sourceArchiveFileInfo.TryExtractBackupDateTime() ?? throw new ArgumentException($"{sourceArchiveFileInfo.Name} doesn't have a date in the filename");
        }

        public static DateTime? TryExtractBackupDateTime(this FileInfo sourceArchiveFileInfo)
        {
            int segment = sourceArchiveFileInfo.Name.LastIndexOf('_');
            if (segment <= 0)
            {
                return null;
            }
            var info = sourceArchiveFileInfo.Name.Substring(segment + 1);
            if (info.Length < 14)
            {
                return null;
            }
            var dt = info.Substring(0, 14);
            if (dt.TryParseArchiveDateTime(out DateTime? tryExtractBackupDateTime))
            {
                return tryExtractBackupDateTime;
            }
            return null;
        }

        public static bool TryParseArchiveDateTime(this string date, out DateTime? tryExtractBackupDateTime)
        {
            if (DateTime.TryParseExact(date, _dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                tryExtractBackupDateTime = parsed.ToUniversalTime();
                return true;
            }
            tryExtractBackupDateTime = null;
            return false;
        }

        private const string _dateFormat = "yyyyMMddHHmmss";
    }
}
