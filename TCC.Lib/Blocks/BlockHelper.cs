using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Blocks
{
    public static class BlockHelper
    {
        public static IEnumerable<Block> PrepareCompressBlocks(CompressOption compressOption)
        {
            IEnumerable<Block> blocks;
            string extension = ExtensionFromAlgo(compressOption.Algo, compressOption.PasswordOption.PasswordMode != PasswordMode.None);
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

            if (!dstDir.Exists)
            {
                dstDir.Create();
            }

            if (compressOption.BackupMode == BackupMode.Full)
            {
                return blocks.Foreach(b => { b.BackupMode = BackupMode.Full; });
            }

            return blocks;
        }

        public static IEnumerable<Block> PrepareDecompressBlocks(DecompressOption decompressOption)
        {
            bool yielded = false;
            var dstDir = new DirectoryInfo(decompressOption.DestinationDir);

            bool crypted = decompressOption.PasswordOption.PasswordMode != PasswordMode.None;

            if (Directory.Exists(decompressOption.SourceDirOrFile))
            {
                var srcDir = new DirectoryInfo(decompressOption.SourceDirOrFile);

                foreach (FileInfo fi in srcDir.EnumerateFiles("*" + ExtensionFromAlgo(CompressionAlgo.Lz4, crypted)))
                {
                    yielded = true;
                    yield return GenerateDecompressBlock(fi, dstDir, CompressionAlgo.Lz4);
                }
                foreach (FileInfo fi in srcDir.EnumerateFiles("*" + ExtensionFromAlgo(CompressionAlgo.Brotli, crypted)))
                {
                    yielded = true;
                    yield return GenerateDecompressBlock(fi, dstDir, CompressionAlgo.Brotli);
                }
                foreach (FileInfo fi in srcDir.EnumerateFiles("*" + ExtensionFromAlgo(CompressionAlgo.Zstd, crypted)))
                {
                    yielded = true;
                    yield return GenerateDecompressBlock(fi, dstDir, CompressionAlgo.Zstd);
                }
            }
            else if (File.Exists(decompressOption.SourceDirOrFile))
            {
                var file = new FileInfo(decompressOption.SourceDirOrFile);
                yielded = true;
                yield return GenerateDecompressBlock(file, dstDir, AlgoFromExtension(file.Extension));
            }

            if (yielded && !dstDir.Exists)
                dstDir.Create();
        }

        private static Block GenerateDecompressBlock(FileInfo sourceFile, DirectoryInfo targetDirectory, CompressionAlgo algo)
        {
            return new Block
            {
                OperationFolder = targetDirectory.FullName,
                Source = sourceFile.FullName,
                DestinationFolder = targetDirectory.FullName,
                ArchiveName = sourceFile.FullName,
                Algo = algo
            };
        }

        private static string ExtensionFromAlgo(CompressionAlgo algo, bool crypted)
        {
            switch (algo)
            {
                case CompressionAlgo.Lz4 when crypted:
                    return ".tarlz4aes";
                case CompressionAlgo.Lz4:
                    return ".tarlz4";
                case CompressionAlgo.Brotli when crypted:
                    return ".tarbraes";
                case CompressionAlgo.Brotli:
                    return ".tarbr";
                case CompressionAlgo.Zstd when crypted:
                    return ".tarzstdaes";
                case CompressionAlgo.Zstd:
                    return ".tarzstd";
                default:
                    throw new ArgumentOutOfRangeException(nameof(algo), algo, null);
            }
        }

        private static CompressionAlgo AlgoFromExtension(string extension)
        {
            switch (extension)
            {
                case ".tarlz4":
                case ".tarlz4aes":
                    return CompressionAlgo.Lz4;
                case ".tarbr":
                case ".tarbraes":
                    return CompressionAlgo.Brotli;
                case ".tarzstd":
                case ".tarzstdaes":
                    return CompressionAlgo.Zstd;
                default:
                    throw new ArgumentOutOfRangeException(nameof(extension), extension, null);
            }
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
            string path;
            string operationFolder;
            string name;

            if (File.Exists(sourceDir))
            {
                var fi = new FileInfo(sourceDir);
                path = fi.Name;
                operationFolder = fi.Directory?.FullName;
                name = Path.GetFileNameWithoutExtension(fi.Name);
            }
            else if (Directory.Exists(sourceDir))
            {
                var di = new DirectoryInfo(sourceDir);
                path = di.Name;
                operationFolder = di.Parent?.FullName;
                name = di.Name;
            }
            else
            {
                throw new FileNotFoundException(nameof(sourceDir), sourceDir);
            }

            yield return new Block
            {
                OperationFolder = operationFolder,
                Source = path,
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
                    OperationFolder = fi.Directory?.FullName,
                    Source = fi.Name,
                    DestinationArchive = Path.Combine(dstDir.FullName, Path.GetFileNameWithoutExtension(fi.Name) + extension),
                    DestinationFolder = dstDir.FullName,
                    ArchiveName = Path.GetFileNameWithoutExtension(fi.Name)
                };
            }
        }
    }
}