using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TCC.Lib.Benchmark
{
    public static class TestFileHelper
    {

        const int BytesToRead = sizeof(Int64);

        public static bool FilesAreEqual(FileInfo first, FileInfo second)
        {
            if (first.Length != second.Length)
                return false;

            if (string.Equals(first.FullName, second.FullName, StringComparison.OrdinalIgnoreCase))
                return true;

            int iterations = (int)Math.Ceiling((double)first.Length / BytesToRead);

            using (FileStream fs1 = first.OpenRead())
            using (FileStream fs2 = second.OpenRead())
            {
                byte[] one = new byte[BytesToRead];
                byte[] two = new byte[BytesToRead];

                for (int i = 0; i < iterations; i++)
                {
                    fs1.Read(one, 0, BytesToRead);
                    fs2.Read(two, 0, BytesToRead);

                    if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0))
                        return false;
                }
            }

            return true;
        }

        public static string NewFileName(string folder)
        {
            var filepath = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".dat");
            return filepath;
        }

        public static async Task<string> NewFile(string folder, int sizeInKb = 1024, bool alphaNumContent = false)
        {
            var filepath = NewFileName(folder);
            if (alphaNumContent)
            {
                await FillRandomFileAlphaNum(filepath, sizeInKb);
            }
            else
            {
                await FillRandomFile(filepath, sizeInKb);
            }
            return filepath;
        }

        public static string NewFolderName(string parentFolder = null)
        {
            var folderPath = Path.Combine(parentFolder ?? Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            return folderPath;
        }

        public static string NewFolder(string parentFolder = null)
        {
            string name = NewFolderName(parentFolder);
            Directory.CreateDirectory(name);
            return name;
        }

        public static async Task FillRandomFile(string fileName, int sizeInKb)
        {
            const int blockSize = 1024;
            const int blocksPerKb = (1024) / blockSize;
            byte[] data = new byte[blockSize];
            var rng = RandomNumberGenerator.Create();
            using (FileStream stream = File.OpenWrite(fileName))
            {
                for (int i = 0; i < sizeInKb * blocksPerKb; i++)
                {
                    rng.GetBytes(data);
                    await stream.WriteAsync(data, 0, data.Length);
                }
            }
        }

        public static async Task FillRandomFileAlphaNum(string fileName, int sizeInKb)
        {
            char[] chars = " abcdef ".ToCharArray();
            const int blockSize = 1024;
            const int blocksPerKb = (1024) / blockSize;

            byte[] data = new byte[blockSize];
            var rng = RandomNumberGenerator.Create();

            using (FileStream stream = File.OpenWrite(fileName))
            {
                for (int i = 0; i < sizeInKb * blocksPerKb; i++)
                {
                    rng.GetBytes(data);
                    byte[] alpha = data.Select(d => (byte)chars[d % chars.Length]).ToArray();
                    await stream.WriteAsync(alpha, 0, alpha.Length);
                }
            }
        }

        public static void FillFile(string fileName, string content)
        {
            Stream stream = null;
            try
            {
                stream = File.OpenWrite(fileName);
                using (var writer = new StreamWriter(stream))
                {
                    writer.WriteLine(content);
                }
            }
            finally
            {
                stream?.Dispose();
            }
        }
    }
}