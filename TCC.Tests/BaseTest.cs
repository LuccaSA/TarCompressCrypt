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
        [Fact]
        public void CompressDecompress()
        {
            var data = TestData.CreateFiles(1, 1);

            var resultCompress = TarCompressCrypt.Compress(data.GetTccCompressOption());

            Assert.Equal(0, resultCompress);

            var dir = new DirectoryInfo(data.Target);
            var decomp = new TestData() { Directories = new List<DirectoryInfo>() { dir } };
            var decompOption = decomp.GetTccDecompressOption();

            var resultDecompress = TarCompressCrypt.Decompress(decompOption);
            Assert.Equal(0, resultDecompress);

            var srcDir = data.Directories.FirstOrDefault();
            var dstDir = new DirectoryInfo(decompOption.DestinationDir);

            Console.WriteLine("TEST : src=" + srcDir.FullName);
            Console.WriteLine("TEST : dst=" + dstDir.FullName);

            FileInfo src = data.Directories.FirstOrDefault().EnumerateFiles().FirstOrDefault();
            FileInfo dst = new DirectoryInfo(decompOption.DestinationDir).EnumerateFiles().FirstOrDefault();

            Assert.True(TestHelper.FilesAreEqual(src, dst));
        }

    }

    public class TestData
    {
        public List<FileInfo> Files { get; set; }
        public List<DirectoryInfo> Directories { get; set; }
        public string Target { get; set; }

        public CompressOption GetTccCompressOption()
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
            Target = TestHelper.NewFolder();

            return new CompressOption
            {
                Individual = true,
                SourceDirOrFile = src,
                DestinationDir = Target,
                Threads = "all",
                PasswordMode = PasswordMode.InlinePassword,
                Password = "1234"
            };
        }

        public DecompressOption GetTccDecompressOption()
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
            Target = TestHelper.NewFolder();

            return new DecompressOption
            {
                SourceDirOrFile = src,
                DestinationDir = Target,
                Threads = "all",
                PasswordMode = PasswordMode.InlinePassword,
                Password = "1234"
            };
        }

        public static TestData CreateFiles(int nbFiles, int sizeMb)
        {
            var tempFolder = TestHelper.NewFolder();
            foreach (var i in Enumerable.Range(0, nbFiles))
            {
                var filePath = TestHelper.NewFileName(tempFolder);
                TestHelper.FillRandomFile(filePath, sizeMb);
                Console.Out.WriteLine("File created : " + filePath);
            }
            Thread.Sleep(150); // for filesystem latency
            return new TestData
            {
                Directories = new List<DirectoryInfo> { new DirectoryInfo(tempFolder) }
            };
        }


    }

    public class TestHelper
    {
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
