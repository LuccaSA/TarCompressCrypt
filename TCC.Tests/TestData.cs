using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TCC.Lib.Benchmark;
using TCC.Lib.Blocks;
using TCC.Lib.Options;

namespace TCC.Tests
{
    public class TestData
    {
        public List<FileInfo> Files { get; set; }
        public List<DirectoryInfo> Directories { get; set; }
        public string Target { get; set; }

        public CompressOption GetTccCompressOption(string targetFolder, CompressionAlgo algo)
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
                Algo = algo,
                BlockMode = BlockMode.Individual,
                SourceDirOrFile = src,
                DestinationDir = Target,
                Threads = Environment.ProcessorCount
            };

            return compressOption;
        }

        public DecompressOption GetTccDecompressOption(string decompressedFolder)
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
                Threads = Environment.ProcessorCount
            };

            return decompressOption;
        }

        public static async Task<TestData> CreateFiles(int nbFiles, int sizeKb, string folder)
        {
            foreach (var _ in Enumerable.Range(0, nbFiles))
            {
                await TestFileHelper.NewFile(folder, sizeKb);
            }
            await Task.Delay(150); // for filesystem latency
            var dir = new DirectoryInfo(folder);
            return new TestData
            {
                Directories = new List<DirectoryInfo> { dir },
                Files = new List<FileInfo>(dir.EnumerateFiles().ToList())
            };
        }


    }
}