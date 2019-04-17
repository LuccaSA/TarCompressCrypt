using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
    }
}
