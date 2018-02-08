using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TCC.Lib.Helpers
{
    public static class FileExtensions
    {
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
