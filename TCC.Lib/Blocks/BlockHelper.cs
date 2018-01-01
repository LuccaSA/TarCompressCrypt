using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TCC.Lib.Blocks
{
    public static class BlockHelper
    {
        public static List<Block> PreprareCompressBlocks(CompressOption compressOption)
        {
            List<Block> blocks;
            string extension = compressOption.PasswordMode != PasswordMode.None ? ".tar.lz4.aes" : ".tar.lz4";
            var srcDir = new DirectoryInfo(compressOption.SourceDirOrFile);
            var dstDir = new DirectoryInfo(compressOption.DestinationDir);

            switch (compressOption.BlockMode)
            {
                case BlockMode.Individual:
                    blocks = PrepareCompressBlockIndividual(extension, srcDir, dstDir).ToList();
                    break;
                case BlockMode.Explicit:
                    blocks = PrepareCompressBlockExplicit(extension, compressOption.SourceDirOrFile, dstDir).ToList();
                    break;
                case BlockMode.EachFile:
                    blocks = PrepareCompressBlockEachFile(extension, srcDir, dstDir).ToList();
                    break;
                case BlockMode.EachFileRecursive:
                    blocks = PrepareCompressBlockEachFileRecursive(extension, srcDir, dstDir).ToList();
                    break;
                default:
                    throw new NotImplementedException();
            }

            if (blocks.Count > 0 && !dstDir.Exists)
            {
                dstDir.Create();
            }

            return blocks;
        }

        public static IEnumerable<Block> PreprareDecompressBlocks(DecompressOption decompressOption)
        {
            bool yielded = false;
            var dstDir = new DirectoryInfo(decompressOption.DestinationDir);

            string extension = decompressOption.PasswordMode != PasswordMode.None ? ".tar.lz4.aes" : ".tar.lz4";

            if (Directory.Exists(decompressOption.SourceDirOrFile))
            {
                var srcDir = new DirectoryInfo(decompressOption.SourceDirOrFile);
                var found = srcDir.EnumerateFiles("*" + extension).ToList();

                foreach (FileInfo fi in found)
                {
                    yielded = true;
                    yield return new Block
                    {
                        OperationFolder = dstDir.FullName,
                        Source = fi.FullName,
                        DestinationFolder = dstDir.FullName,
                        ArchiveName = fi.FullName
                    };
                }
            }
            else if (File.Exists(decompressOption.SourceDirOrFile) && Path.HasExtension(extension))
            {
                yielded = true;
                yield return new Block
                {
                    OperationFolder = dstDir.FullName,
                    Source = decompressOption.SourceDirOrFile,
                    DestinationFolder = dstDir.FullName,
                    ArchiveName = new FileInfo(decompressOption.SourceDirOrFile).FullName
                };
            }

            if (yielded && !dstDir.Exists)
                dstDir.Create();
        }

        private static IEnumerable<Block> PrepareCompressBlockIndividual(string extension, DirectoryInfo srcDir, DirectoryInfo dstDir)
        {
            // for each directory in sourceDir we create an archive
            foreach (DirectoryInfo di in srcDir.EnumerateDirectories())
            {
                yield return new Block
                {
                    OperationFolder = srcDir.FullName,
                    Source = di.Name,
                    DestinationArchive = Path.Combine(dstDir.FullName, di.Name + extension),
                    DestinationFolder = dstDir.FullName,
                    ArchiveName = di.Name
                };
            }

            // for each file in sourceDir we create an archive
            foreach (FileInfo fi in srcDir.EnumerateFiles())
            {
                yield return new Block
                {
                    OperationFolder = srcDir.FullName,
                    Source = fi.Name,
                    DestinationArchive = Path.Combine(dstDir.FullName, Path.GetFileNameWithoutExtension(fi.Name) + extension),
                    DestinationFolder = dstDir.FullName,
                    ArchiveName = Path.GetFileNameWithoutExtension(fi.Name)
                };
            }
        }

        private static IEnumerable<Block> PrepareCompressBlockExplicit(string extension, string sourceDir, DirectoryInfo dstDir)
        {
            string fullPath;
            string operationFolder;
            string name;

            if (File.Exists(sourceDir))
            {
                var fi = new FileInfo(sourceDir);
                fullPath = fi.FullName;
                operationFolder = fi.Directory.FullName;
                name = Path.GetFileNameWithoutExtension(fi.Name);
            }
            else if (Directory.Exists(sourceDir))
            {
                var di = new DirectoryInfo(sourceDir);
                fullPath = di.FullName;
                operationFolder = di.Parent.FullName;
                name = di.Name;
            }
            else
            {
                throw new FileNotFoundException(nameof(sourceDir), sourceDir);
            }

            yield return new Block
            {
                OperationFolder = operationFolder,
                Source = fullPath,
                DestinationArchive = Path.Combine(dstDir.FullName, name + extension),
                DestinationFolder = dstDir.FullName,
                ArchiveName = name
            };
        }

        private static IEnumerable<Block> PrepareCompressBlockEachFile(string extension, DirectoryInfo srcDir, DirectoryInfo dstDir)
        {
            return BlockFiles(extension, srcDir, dstDir, SearchOption.TopDirectoryOnly);
        }

        private static IEnumerable<Block> PrepareCompressBlockEachFileRecursive(string extension, DirectoryInfo srcDir, DirectoryInfo dstDir)
        {
            return BlockFiles(extension, srcDir, dstDir, SearchOption.AllDirectories);
        }

        private static IEnumerable<Block> BlockFiles(string extension, DirectoryInfo srcDir, DirectoryInfo dstDir, SearchOption option)
        {
            // for each file in sourceDir we create an archive
            foreach (FileInfo fi in srcDir.EnumerateFiles("*", option))
            {
                yield return new Block
                {
                    OperationFolder = fi.Directory.FullName,
                    Source = fi.Name,
                    DestinationArchive = Path.Combine(dstDir.FullName, Path.GetFileNameWithoutExtension(fi.Name) + extension),
                    DestinationFolder = dstDir.FullName,
                    ArchiveName = Path.GetFileNameWithoutExtension(fi.Name)
                };
            }
        }
    }
}