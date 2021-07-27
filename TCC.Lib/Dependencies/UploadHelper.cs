using System;
using System.IO;

namespace TCC.Lib.Dependencies
{
    public static class UploadHelper
    {
        public static string GetRelativeTargetPathTo(this FileInfo file, DirectoryInfo root )
        {
            var targetPath = file.FullName.Replace(root.FullName, string.Empty, StringComparison.InvariantCultureIgnoreCase)
                .Trim('\\').Trim('/');

            targetPath = targetPath.Replace('\\', '/');

            return targetPath;
        }
    }
}