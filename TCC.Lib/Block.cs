using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TCC
{
    public class Block
    {
        public string OperationFolder { get; set; }
        public string Source { get; set; }
        public string DestinationArchive { get; set; }
        public string DestinationFolder { get; set; }
        public string ArchiveName { get; set; }
    }

    public enum BlockMode
    {
        /// <summary>
        /// Each explicit file or folder in input create an output archive
        /// </summary>
        Explicit,
        /// <summary>
        /// Each folder is parsed, and foreach file or folder in this input folder, we create an archive
        /// </summary>
        Individual,
        /// <summary>
        /// Each file in a folder is archived
        /// </summary>
        EachFile,
        /// <summary>
        /// Each file in a folder and subfolder is archived
        /// </summary>
        EachFileRecursive
    }

    public static class BlockHelper
    {
        public static List<Block> PreprareDecompressBlocks(string sourceDir, string destinationDir, bool crypt)
        {
            var blocks = new List<Block>();
            string extension = crypt ? ".tar.lz4.aes" : ".tar.lz4";
            if (Directory.Exists(sourceDir))
            {
                var srcDir = new DirectoryInfo(sourceDir);
                var found = srcDir.EnumerateFiles("*" + extension).ToList();
                var dstDir = new DirectoryInfo(destinationDir);
                if (found.Count != 0)
                {
                    if (dstDir.Exists == false)
                    {
                        dstDir.Create();
                    }
                }

                foreach (FileInfo fi in found)

                {
                    blocks.Add(new Block
                    {
                        OperationFolder = dstDir.FullName,
                        Source = fi.FullName,
                        DestinationFolder = dstDir.FullName,
                        ArchiveName = fi.FullName
                    });
                }
            }
            else if (File.Exists(sourceDir) && Path.HasExtension(extension))
            {
                var dstDir = new DirectoryInfo(destinationDir);
                if (dstDir.Exists == false)
                {
                    dstDir.Create();
                }
                blocks.Add(new Block
                {
                    OperationFolder = dstDir.FullName,
                    Source = sourceDir,
                    DestinationFolder = dstDir.FullName,
                    ArchiveName = new FileInfo(sourceDir).FullName
                });
            }
            return blocks;
        }

        public static List<Block> PreprareCompressBlocks(string sourceDir, string destinationDir, BlockMode blockMode, bool crypt)
        {
            List<Block> blocks = null;
            string extension = crypt ? ".tar.lz4.aes" : ".tar.lz4";
            var srcDir = new DirectoryInfo(sourceDir);
            var dstDir = new DirectoryInfo(destinationDir);

            switch (blockMode)
            {
                case BlockMode.Individual:
                    blocks = PrepareCompressBlockIndividual(extension, srcDir, dstDir);
                    break;
                case BlockMode.Explicit:
                    blocks = PrepareCompressBlockExplicit(extension, sourceDir, dstDir);
                    break;
                case BlockMode.EachFile:
                    blocks = PrepareCompressBlockEachFile(extension, srcDir, dstDir);
                    break;
                case BlockMode.EachFileRecursive:
                    blocks = PrepareCompressBlockEachFileRecursive(extension, srcDir, dstDir);
                    break;
                default:
                    throw new NotImplementedException();
            }

            return blocks;
        }

        private static List<Block> PrepareCompressBlockIndividual(string extension, DirectoryInfo srcDir, DirectoryInfo dstDir)
        {
            var blocks = new List<Block>();
            List<FileInfo> files = srcDir.EnumerateFiles().ToList();
            List<DirectoryInfo> directories = srcDir.EnumerateDirectories().ToList();

            if (files.Count != 0 || directories.Count != 0)
            {
                if (!dstDir.Exists)
                {
                    dstDir.Create();
                }
            }

            // for each directory in sourceDir we create an archive
            foreach (DirectoryInfo di in directories)
            {
                blocks.Add(new Block
                {
                    OperationFolder = srcDir.FullName,
                    Source = di.Name,
                    DestinationArchive = Path.Combine(dstDir.FullName, di.Name + extension),
                    DestinationFolder = dstDir.FullName,
                    ArchiveName = di.Name
                });
            }

            // for each file in sourceDir we create an archive
            foreach (FileInfo fi in files)
            {
                blocks.Add(new Block
                {
                    OperationFolder = srcDir.FullName,
                    Source = fi.Name,
                    DestinationArchive = Path.Combine(dstDir.FullName, Path.GetFileNameWithoutExtension(fi.Name) + extension),
                    DestinationFolder = dstDir.FullName,
                    ArchiveName = Path.GetFileNameWithoutExtension(fi.Name)
                });
            }

            return blocks;
        }


        private static List<Block> PrepareCompressBlockExplicit(string extension, string sourceDir, DirectoryInfo dstDir)
        {
            var blocks = new List<Block>();
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

            blocks.Add(new Block
            {
                OperationFolder = operationFolder,
                Source = fullPath,
                DestinationArchive = Path.Combine(dstDir.FullName, name + extension),
                DestinationFolder = dstDir.FullName,
                ArchiveName = name
            });
            return blocks;
        }

        private static List<Block> PrepareCompressBlockEachFile(string extension, DirectoryInfo srcDir, DirectoryInfo dstDir)
        {
            throw new NotImplementedException();
        }

        private static List<Block> PrepareCompressBlockEachFileRecursive(string extension, DirectoryInfo srcDir, DirectoryInfo dstDir)
        {
            throw new NotImplementedException();
        }
    }
}