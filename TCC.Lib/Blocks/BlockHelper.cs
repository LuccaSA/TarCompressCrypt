using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using TCC.Lib.Database;
using TCC.Lib.Helpers;
using TCC.Lib.Options;
using TCC.Lib.Storage;

namespace TCC.Lib.Blocks
{
    public static class BlockHelper
    {
        public static IEnumerable<CompressionBlock> GenerateCompressBlocks(this CompressOption compressOption,
            CompressionFolderProvider compressionFolderProvider)
        {
            IEnumerable<CompressionBlock> blocks;
            string extension = ExtensionFromAlgo(compressOption.Algo, compressOption.PasswordOption.PasswordMode != PasswordMode.None);

            DirectoryInfo srcDir;
            if (File.Exists(compressOption.SourceDirOrFile))
            {
                srcDir = new FileInfo(compressOption.SourceDirOrFile).Directory;
            }
            else
            {
                srcDir = new DirectoryInfo(compressOption.SourceDirOrFile);
            }

            var compFolder = new CompressionFolderProvider(new DirectoryInfo(compressOption.DestinationDir), compressOption.FolderPerDay);

            switch (compressOption.BlockMode)
            {
                case BlockMode.Individual:
                    blocks = PrepareCompressBlockIndividual(srcDir);
                    break;
                case BlockMode.Explicit:
                    blocks = PrepareCompressBlockExplicit(compressOption.SourceDirOrFile);
                    break;
                default:
                    throw new NotImplementedException();
            }

            HashSet<string> filters = null;
            if (compressOption.Filter != null && compressOption.Filter.Any())
            {
                filters = compressOption.Filter.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            HashSet<string> excludes = null;
            if (compressOption.Exclude != null && compressOption.Exclude.Any())
            {
                excludes = compressOption.Exclude.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var block in blocks)
            {
                if (filters != null && !filters.Contains(block.BlockName))
                {
                    continue;
                }

                if (excludes != null && excludes.Contains(block.BlockName))
                {
                    continue;
                }
                block.DestinationArchiveExtension = extension;
                block.FolderProvider = compFolder;
                yield return block;
            }
        }

        private static readonly string[] _extensions = { "*.tarlz4aes", "*.tarlz4", "*.tarbraes", "*.tarbr", "*.tarzstdaes", "*.tarzstd" };

        public static async IAsyncEnumerable<DecompressionBatch> GenerateDecompressBlocksAsync(
            this DecompressOption decompressOption, ILogger logger, [EnumeratorCancellation] CancellationToken token)
        {
            bool yielded = false;
            var dstDir = new DirectoryInfo(decompressOption.DestinationDir);

            if (decompressOption is RetrieveOptions retrieveOptions)
            {
                await foreach(var batch in retrieveOptions.GenerateDecompressBatchesAsync(logger, token))
                {
                    yielded = true;
                    yield return batch;
                }
                if (yielded && !dstDir.Exists)
                    dstDir.Create();
                yield break;
            }

            // If file -> direct decompress

            if (!Directory.Exists(decompressOption.SourceDirOrFile))
            {
                if (File.Exists(decompressOption.SourceDirOrFile))
                {
                    var file = new FileInfo(decompressOption.SourceDirOrFile);
                    yielded = true;
                    yield return new DecompressionBatch
                    {
                        BackupFull = GenerateDecompressBlock(file, dstDir, AlgoFromExtension(file.Extension))
                    };
                }
                else
                {
                    throw new ArgumentException($"The path provided contains no archive at all : {decompressOption.SourceDirOrFile}");
                }
            }
            else
            {
                // If directory

                var srcDir = new DirectoryInfo(decompressOption.SourceDirOrFile);
                var norecurse = new EnumerationOptions { RecurseSubdirectories = false };
                var archivesFound = _extensions.SelectMany(i => srcDir.EnumerateFiles(i, norecurse));

                if (archivesFound.Any())
                {
                    // -> Multiple archives in directory
                    //    -> Only FULL extensions
                    //    -> FULL and DIFF extensions
                    //    -> Archive extensions 
                    throw new NotImplementedException();
                }
                else
                {
                    var fullBackups = new Dictionary<string, (DateTime fullDate, DecompressionBatch batch, long fullsize)>();

                    // -> Multiple directories in directory
                    //    -> Each directory should contains FULL and DIFF folders
                    foreach (var dir in srcDir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                    {
                        var diff = dir.EnumerateDirectories(TccConst.Diff).FirstOrDefault();
                        var full = dir.EnumerateDirectories(TccConst.Full).FirstOrDefault();

                        if (diff == null && full == null)
                        {
                            foreach (FileInfo fi in dir.EnumerateArchives())
                            {
                                yielded = true;
                                yield return new DecompressionBatch
                                {
                                    BackupFull = GenerateDecompressBlock(fi, dstDir, AlgoFromExtension(fi.Extension))
                                };
                            }
                        }
                        else
                        {
                            // at least a FULL or a DIFF folder
                            var lastFull = full?.EnumerateArchives().OrderByDescending(i => i.ExtractBackupDateTime()).FirstOrDefault();

                            if (lastFull != null)
                            {
                                var batch = new DecompressionBatch();
                                fullBackups.Add(dir.Name, (lastFull.ExtractBackupDateTime(), batch, lastFull.Length));
                                yielded = true;
                                batch.BackupFull = GenerateDecompressBlock(lastFull, dstDir, AlgoFromExtension(lastFull.Extension));
                            }

                            if (diff == null)
                            {
                                continue;
                            }

                            if (fullBackups.TryGetValue(dir.Name, out var datedBatch))
                            {
                                var diffs = new List<DecompressionBlock>();
                                foreach (var diffArchive in diff.EnumerateArchives()
                                    .Where(i => i.ExtractBackupDateTime() >= datedBatch.fullDate)
                                    .OrderBy(i => i.ExtractBackupDateTime()))
                                {
                                    yielded = true;
                                    diffs.Add(GenerateDecompressBlock(diffArchive, dstDir, AlgoFromExtension(diffArchive.Extension)));
                                }
                                datedBatch.batch.BackupsDiff = diffs.ToArray();
                            }
                            else
                            {
                                if (decompressOption.IgnoreMissingFull)
                                {
                                    yielded = true;
                                    var batch = new DecompressionBatch();

                                    var diffs = new List<DecompressionBlock>();
                                    foreach (var diffArchive in diff.EnumerateArchives()
                                        .Where(i => i.ExtractBackupDateTime() >= datedBatch.fullDate)
                                        .OrderBy(i => i.ExtractBackupDateTime()))
                                    {
                                        yielded = true;
                                        diffs.Add(GenerateDecompressBlock(diffArchive, dstDir, AlgoFromExtension(diffArchive.Extension)));
                                    }
                                    batch.BackupsDiff = diffs.ToArray();
                                    fullBackups.Add(dir.Name, (DateTime.MinValue, batch, 0));
                                }
                                else
                                {
                                    throw new Exception("Missing FULL backup, and DIFF found in directory " + dir.FullName);
                                }
                            }
                        }
                    }

                    foreach (var found in fullBackups.Values.OrderByDescending(i => i.fullsize))
                    {
                        yield return found.batch;
                    }
                }
            }

            if (yielded && !dstDir.Exists)
                dstDir.Create();
        }
        public static string GetRemoteStorageKey(this DecompressionBlock block,
            DirectoryInfo retrieveOptionsDownloadDestinationDir)
        {
            return block.SourceArchiveFileInfo.FullName.Substring(retrieveOptionsDownloadDestinationDir.FullName.Length + 1);
        }

        private static async IAsyncEnumerable<DecompressionBatch> GenerateDecompressBatchesAsync(this RetrieveOptions options, ILogger logger, [EnumeratorCancellation] CancellationToken token)
        {
            var downloader = await options.GetRemoteStorageAsync(logger, token);

            Console.WriteLine(options.SourceMachine);
            Console.WriteLine(string.IsNullOrWhiteSpace(options.SourceArchive) ? "No source specified: Retrieve all available backups for given machine" : options.SourceArchive);
            Console.WriteLine(options.BeforeDateTime);
            Console.WriteLine(options.FolderPerDay);
            Console.WriteLine(options.All ? "Retrieve all backups" : "Retrieve last backup");

            var archiveList = downloader.ListArchivesMatchingWithSizeAsync(options, token)
                .GroupBy(key =>
                {
                    var fileInfos = new FileInfo(key.Key);
                    //TODO Qqchose quand on a le folderPerDate à true à va fouttre la merde

                    return fileInfos.Name.Split("_").First();
                });

            await foreach(var grouping in archiveList)
            {
                var batch =  new DownloadBatch()
                {
                    BackupFull = new DecompressionBlock(), BackupsDiff = new DecompressionBlock[] { }, CompressedSize = 0
                };
                await foreach (var archive in grouping)
                {
                    var pathToUse = archive.Key.Replace('/', Path.DirectorySeparatorChar);
                    var downloadFileInfos = new FileInfo(Path.Combine(options.DownloadDestinationDir.FullName, pathToUse));

                    var block = new DecompressionBlock()
                    {
                        SourceArchiveFileInfo = downloadFileInfos,
                        OperationFolder = options.DecryptionDestinationDir.FullName,
                        Algo = AlgoFromExtension(downloadFileInfos.Extension)
                    };
                    
                    if (downloadFileInfos.Name.Split(".")[^2] == TccConst.Full.ToLowerInvariant())
                    {
                        batch.BackupFull = block;
                    }
                    else
                    {
                        batch.BackupsDiff = batch.BackupsDiff.Append(block).ToArray();
                    }
                    batch.CompressedSize += archive.Size;
                }
                yield return batch;
            }
        }


        private static DecompressionBlock GenerateDecompressBlock(FileInfo sourceFile, DirectoryInfo targetDirectory, CompressionAlgo algo)
        {
            return new DecompressionBlock
            {
                OperationFolder = targetDirectory.FullName,
                SourceArchiveFileInfo = sourceFile,
                Algo = algo
            };
        }

        private static IEnumerable<FileInfo> EnumerateArchives(this DirectoryInfo directoryInfo)
        {
            foreach (CompressionAlgo algo in Enum.GetValues(typeof(CompressionAlgo)))
            {
                foreach (FileInfo fi in directoryInfo.EnumerateFiles("*" + ExtensionFromAlgo(algo, true)))
                {
                    yield return fi;
                }

                foreach (FileInfo fi in directoryInfo.EnumerateFiles("*" + ExtensionFromAlgo(algo, false)))
                {
                    yield return fi;
                }
            }
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

        private static IEnumerable<CompressionBlock> PrepareCompressBlockIndividual(DirectoryInfo srcDir)
        {
            // for each directory in sourceDir we create an archive
            foreach (DirectoryInfo di in srcDir.EnumerateDirectories())
            {
                yield return new CompressionBlock
                {
                    SourceFileOrDirectory = new FileOrDirectoryInfo(di),
                    SourceOperationFolder = srcDir
                };
            }

            // for each file in sourceDir we create an archive
            foreach (FileInfo fi in srcDir.EnumerateFiles())
            {
                if (fi.Name == "tcc.db")
                {
                    continue;
                }
                if (fi.Name.StartsWith("tcc") && fi.Name.EndsWith(".log"))
                {
                    continue;
                }

                yield return new CompressionBlock
                {
                    SourceFileOrDirectory = new FileOrDirectoryInfo(fi),
                    SourceOperationFolder = srcDir
                };
            }
        }

        private static IEnumerable<CompressionBlock> PrepareCompressBlockExplicit(string sourceDir)
        {
            var fod = new FileOrDirectoryInfo(sourceDir);

            var rootDir = fod.Kind == SourceKind.Directory
                ? fod.DirectoryInfo?.Parent
                : fod.FileInfo?.Directory;

            if (rootDir == null)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceDir), "Invalid path provided");
            }

            yield return new CompressionBlock
            {
                SourceFileOrDirectory = fod,
                SourceOperationFolder = rootDir
            };
        }
    }
}