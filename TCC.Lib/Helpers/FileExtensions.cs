using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TCC.Lib.Helpers
{
    public static class FileExtensions
    {
        public static Task Lock(this FileInfo lockFilePath, Func<Task> action)
        {
            if (lockFilePath == null) throw new ArgumentNullException(nameof(lockFilePath));
            if (action == null) throw new ArgumentNullException(nameof(action));

            return LockInternal(lockFilePath, action);
        }

        private static async Task LockInternal(FileInfo lockFilePath, Func<Task> action)
        {
            while (true)
            {
                try
                {
                    using (File.Open(lockFilePath.FullName,
                        FileMode.OpenOrCreate,
                        FileAccess.Read,
                        FileShare.None))
                    {
                        try
                        {
                            await action();
                        }
                        catch (Exception)
                        {
                            // prevent orphan lock file
                        }
                    }
                    await lockFilePath.FullName.TryDeleteFileWithRetryAsync();
                    break;
                }
                catch (IOException)
                {
                    // lock file exists
                }
                await Task.Delay(1000);
            }
        }

        public static long GetDirectoryOrFileSize(this string source)
        {
            if (File.GetAttributes(source).HasFlag(FileAttributes.Directory))
            {
                return new DirectoryInfo(source).GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
            }
            else
            {
                return new FileInfo(source).Length;
            }
        }

        public static void CreateEmptyFile(this string filePath)
        {
            using (File.Open(filePath, FileMode.OpenOrCreate))
            {
                // empty file
            }
        }

        public static Task TryDeleteFileWithRetryAsync(this string filePath, int retries = 100)
        {
            if (retries <= 1)
                throw new ArgumentOutOfRangeException(nameof(retries));

            return TryDeleteFileWithRetryInternalAsync(filePath, retries);
        }

        private static async Task TryDeleteFileWithRetryInternalAsync(string filePath, int retries)
        {
            retries--;
            while (File.Exists(filePath) && retries >= 0)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception)
                {
                    // Exceptions are ignored
                }
                await Task.Delay(10);
                retries--;
            }
            // last try, we let throw exception
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            
        }
    }
}
