using System;
using System.IO;
using System.Security.Cryptography;

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

        public static string NewFile(string folder, int sizeInMb = 1)
        {
            var filepath = NewFileName(folder);
            FillRandomFile(filepath, sizeInMb);
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

        public static void FillRandomFile(string fileName, int sizeInMb)
        {
            const int blockSize = 1024 * 8;
            const int blocksPerMb = (1024 * 1024) / blockSize;
            byte[] data = new byte[blockSize];
            var rng = RandomNumberGenerator.Create();
            using (FileStream stream = File.OpenWrite(fileName))
            {
                // There 
                for (int i = 0; i < sizeInMb * blocksPerMb; i++)
                {
                    rng.GetBytes(data);
                    stream.Write(data, 0, data.Length);
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