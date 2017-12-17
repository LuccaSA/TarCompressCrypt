using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TCC.Tests
{
    public class BaseTest
    {
        [Theory]
        //[InlineData(PasswordMode.None)]
        [InlineData(PasswordMode.InlinePassword)]
        //[InlineData(PasswordMode.PasswordFile)]
        [InlineData(PasswordMode.PublicKey)]
        public void CompressDecompress(PasswordMode mode)
        {
            string toCompressFolder = TestHelper.NewFolder();
            string compressedFolder = TestHelper.NewFolder();
            string decompressedFolder = TestHelper.NewFolder();
            string keysFolder = TestHelper.NewFolder();

            var data = TestData.CreateFiles(1, 1, toCompressFolder);
            var compressOption = data.GetTccCompressOption(mode, compressedFolder);

            switch (mode)
            {
                case PasswordMode.None:
                    break;
                case PasswordMode.InlinePassword:
                    compressOption.Password = "1234";
                    break;
                case PasswordMode.PasswordFile:
                    break;
                case PasswordMode.PublicKey:
                    {
                        var p1 = TestHelper.CreateKeyPairCommand("keypair.pem", KeySize.Key4096).Run(keysFolder, CancellationToken.None);
                        var p2 = TestHelper.CreatePublicKeyCommand("keypair.pem", "public.pem").Run(keysFolder, CancellationToken.None);
                        var p3 = TestHelper.CreatePrivateKeyCommand("keypair.pem", "private.pem").Run(keysFolder, CancellationToken.None);

                        compressOption.PublicPrivateKeyFile = Path.Combine(keysFolder, "public.pem");
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            var resultCompress = TarCompressCrypt.Compress(compressOption);

            Assert.Equal(0, resultCompress);

            var decomp = new TestData { Directories = new List<DirectoryInfo> { new DirectoryInfo(compressedFolder) } };

            var decompOption = decomp.GetTccDecompressOption(mode, decompressedFolder);

            switch (mode)
            {
                case PasswordMode.None:
                    break;
                case PasswordMode.InlinePassword:
                    decompOption.Password = "1234";
                    break;
                case PasswordMode.PasswordFile:
                    break;
                case PasswordMode.PublicKey:
                    decompOption.PublicPrivateKeyFile = Path.Combine(keysFolder, "private.pem");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            var resultDecompress = TarCompressCrypt.Decompress(decompOption);
            Assert.Equal(0, resultDecompress);
             
            Console.WriteLine("TEST : src=" + toCompressFolder);
            Console.WriteLine("TEST : dst=" + decompressedFolder);

            FileInfo src = new DirectoryInfo(toCompressFolder).EnumerateFiles().FirstOrDefault();
            FileInfo dst = new DirectoryInfo(decompressedFolder).EnumerateFiles().FirstOrDefault();

            Assert.True(TestHelper.FilesAreEqual(src, dst));
        }

    }

    public class TestData
    {
        public List<FileInfo> Files { get; set; }
        public List<DirectoryInfo> Directories { get; set; }
        public string Target { get; set; }

        public CompressOption GetTccCompressOption(PasswordMode passwordMode, string targetFolder)
        {
            string src;
            if (Files != null)
            {
                src = String.Join(" ", Files.Select(i => i.FullName));
            }
            else if (Directories != null)
            {
                src = String.Join(" ", Directories.Select(i => i.FullName));
            }
            else
            {
                throw new MissingMemberException();
            }
            Target = targetFolder;

            var compressOption = new CompressOption
            {
                Individual = true,
                SourceDirOrFile = src,
                DestinationDir = Target,
                Threads = "all",
                PasswordMode = passwordMode,
                Password = "1234"
            };


            return compressOption;
        }

        public DecompressOption GetTccDecompressOption(PasswordMode passwordMode, string decompressedFolder)
        {
            string src;
            if (Files != null)
            {
                src = String.Join(" ", Files.Select(i => i.FullName));
            }
            else if (Directories != null)
            {
                src = String.Join(" ", Directories.Select(i => i.FullName));
            }
            else
            {
                throw new MissingMemberException();
            }
            Target = decompressedFolder;

            var decompressOption = new DecompressOption
            {
                SourceDirOrFile = src,
                DestinationDir = Target,
                Threads = "all",
                PasswordMode = passwordMode
            };


            return decompressOption;
        }

        public static TestData CreateFiles(int nbFiles, int sizeMb, string folder)
        {
            foreach (var i in Enumerable.Range(0, nbFiles))
            {
                var filePath = TestHelper.NewFileName(folder);
                TestHelper.FillRandomFile(filePath, sizeMb);
                Console.Out.WriteLine("File created : " + filePath);
            }
            Thread.Sleep(150); // for filesystem latency
            return new TestData
            {
                Directories = new List<DirectoryInfo> { new DirectoryInfo(folder) }
            };
        }


    }

    public enum KeySize
    {
        Key4096 = 4096,
        Key8192 = 8192
    }

    public class TestHelper
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

        public static string NewFolderName()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            return folderPath;
        }

        public static string NewFolder()
        {
            string name = NewFolderName();
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
    }
}
