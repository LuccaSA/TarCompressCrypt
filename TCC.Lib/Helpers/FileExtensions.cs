using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TCC.Lib.Helpers
{
    public static class FileExtensions
    {
        public static async Task Lock(this FileInfo lockFilePath, Func<Task> action)
        {
            if (lockFilePath == null) throw new ArgumentNullException(nameof(lockFilePath));
            if (action == null) throw new ArgumentNullException(nameof(action));

            using (var autoResetEvent = new AutoResetEvent(false))
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
                        File.Delete(lockFilePath.FullName);
                        break;
                    }
                    catch (IOException)
                    {
                        try
                        {
                            var fileSystemWatcher =
                                new FileSystemWatcher(Path.GetDirectoryName(lockFilePath.FullName))
                                {
                                    EnableRaisingEvents = true
                                };
                            fileSystemWatcher.Changed +=
                                (o, e) =>
                                {
                                    if (Path.GetFullPath(e.FullPath) == Path.GetFullPath(lockFilePath.FullName))
                                    {
                                        autoResetEvent.Set();
                                    }
                                };
                            if (File.Exists(lockFilePath.FullName))
                            {
                                autoResetEvent.WaitOne();
                            }
                        }
                        catch (Exception)
                        {
                            // Catch possible WaitOne() exceptions
                        }
                    }
                }
            }
        }

        public static void CreateEmptyFile(this string filePath)
        {
            using (File.Open(filePath, FileMode.OpenOrCreate))
            {
                // empty file
            }
        }

        public static void TryDeleteFileWithRetry(this string filePath, int retries = 100)
        {
            if (retries <= 1)
                throw new ArgumentOutOfRangeException(nameof(retries));

            retries--;
            while (File.Exists(filePath) && retries > 0)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception)
                {
                    // Exceptions are ignored
                }
                Thread.Sleep(10);
                retries--;
            }

            // last try, we let throw exception
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }


        public static async Task TryDeleteFileWithRetryAsync(this string filePath, int retries = 100)
        {
            if (retries <= 1)
                throw new ArgumentOutOfRangeException(nameof(retries));

            retries--;
            while (File.Exists(filePath) && retries > 0)
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
