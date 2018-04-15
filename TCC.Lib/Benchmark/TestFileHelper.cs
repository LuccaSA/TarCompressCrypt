using System;
using System.IO;

namespace TCC.Lib.Benchmark
{
    public static class TestFileHelper
    {
        public static string CreateKeyPairCommand(string keyPairFile, KeySize keySize)
            => $"openssl genpkey -algorithm RSA -out {keyPairFile} -pkeyopt rsa_keygen_bits:{(int)keySize}";

        public static string CreatePublicKeyCommand(string keyPairFile, string publicKeyFile)
            => $"openssl rsa -pubout -outform PEM -in {keyPairFile} -out {publicKeyFile}";

        public static string CreatePrivateKeyCommand(string keyPairFile, string privateKeyFile)
            => $"openssl rsa -outform PEM -in {keyPairFile} -out {privateKeyFile}";

        const int BYTES_TO_READ = sizeof(Int64);

        public static bool FilesAreEqual(FileInfo first, FileInfo second)
        {
            if (first.Length != second.Length)
                return false;

            if (string.Equals(first.FullName, second.FullName, StringComparison.OrdinalIgnoreCase))
                return true;

            int iterations = (int)Math.Ceiling((double)first.Length / BYTES_TO_READ);

            using (FileStream fs1 = first.OpenRead())
            using (FileStream fs2 = second.OpenRead())
            {
                byte[] one = new byte[BYTES_TO_READ];
                byte[] two = new byte[BYTES_TO_READ];

                for (int i = 0; i < iterations; i++)
                {
                    fs1.Read(one, 0, BYTES_TO_READ);
                    fs2.Read(two, 0, BYTES_TO_READ);

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
            Random rng = new Random();
            using (FileStream stream = File.OpenWrite(fileName))
            {
                // There 
                for (int i = 0; i < sizeInMb * blocksPerMb; i++)
                {
                    rng.NextBytes(data);
                    stream.Write(data, 0, data.Length);
                }
            }
        }

        public static void FillFile(string fileName, string content)
        {
            using (FileStream stream = File.OpenWrite(fileName))
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.WriteLine(content);
                }
            }
        }
    }
}