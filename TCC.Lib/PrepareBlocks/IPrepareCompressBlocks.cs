using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TCC.Lib.Blocks;
using TCC.Lib.Database;
using TCC.Lib.Helpers;

namespace TCC.Lib.PrepareBlocks
{
    public interface IPrepareCompressBlocks
    {
        public List<CompressionBlock> PrepareCompressionBlocksAsync(IEnumerable<CompressionBlock> blocks);
    }

    public class FileSystemPrepareCompressBlocks : IPrepareCompressBlocks
    {
        private readonly CompressionFolderProvider _compressionFolderProvider;
        private readonly BackupMode? _backupMode;
        private readonly int? _cleanupTime;

        public FileSystemPrepareCompressBlocks(CompressionFolderProvider compressionFolderProvider,
            BackupMode? backupMode, int? cleanupTime)
        {
            _compressionFolderProvider = compressionFolderProvider;
            _backupMode = backupMode;
            _cleanupTime = cleanupTime;
        }

        public List<CompressionBlock> PrepareCompressionBlocksAsync(IEnumerable<CompressionBlock> blocks)
        {
            Dictionary<string, List<DirectoryInfo>> fulls;
            var hostname = _compressionFolderProvider.RootFolder.Hostname();

            // find fulls
            if (_compressionFolderProvider.FolderPerDay)
            {
                // root/date/hostname/item
                fulls = _compressionFolderProvider.RootFolder.EnumerateDirectories()
                    .Where(d => IsValidDate(d.Name))
                    .SelectMany(d => d.EnumerateDirectories(hostname))
                    .SelectMany(d => d.EnumerateDirectories())
                    .GroupBy(i => i.Name)
                    .ToDictionary(i => i.Key, v => v.ToList());
            }
            else
            {
                // root/hostname/item
                fulls = _compressionFolderProvider.RootFolder
                    .EnumerateDirectories(hostname)
                    .SelectMany(d => d.EnumerateDirectories())
                    .GroupBy(i => i.Name)
                    .ToDictionary(i => i.Key, v => v.ToList());
            }

            var bag = new ConcurrentBag<BlockSized>();
            Parallel.ForEach(blocks, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                (b) =>
            {
                long size = 0;
                // default value : no full found, force a new full
                b.BackupMode = BackupMode.Full;

                if (fulls.TryGetValue(b.BlockName, out var directories))
                {
                    var fullFiles = directories
                        .SelectMany(d => d.GetDirectories(TccConst.Full))
                        .SelectMany(d => d.EnumerateFiles())
                        .Select(f => new BlockFileWithTime { File = f, Date = ParseDateTime(f.Name) })
                        .ToList();

                    DateTime? dtLimit = null;
                    if (_cleanupTime.HasValue)
                    {
                        dtLimit = DateTime.UtcNow - TimeSpan.FromHours(_cleanupTime.Value);
                    }

                    if (dtLimit.HasValue)
                    {
                        b.FullsToDelete = fullFiles
                            .Where(f => f.Date != null && f.Date.Value < dtLimit.Value)
                            .Select(f => f.File)
                            .ToList();

                        fullFiles = fullFiles
                            .Where(f => f.Date == null || f.Date.Value >= dtLimit.Value)
                            .ToList();
                    }

                    BlockFileWithTime mostRecentFull = fullFiles
                        .OrderByDescending(x => x.Date)
                        .FirstOrDefault();

                    if (mostRecentFull != null)
                    {
                        size = mostRecentFull.File.Length;

                        if (_backupMode == null || _backupMode.Value == BackupMode.Diff)
                        {
                            // full found, get the last diff date
                            var diffFiles = directories
                                .SelectMany(d => d.GetDirectories(TccConst.Diff))
                                .SelectMany(d => d.EnumerateFiles())
                                .Where(i => i.Length != 0)
                                .Select(f => new BlockFileWithTime { File = f, Date = ParseDateTime(f.Name) })
                                .ToList();

                            if (dtLimit.HasValue)
                            {
                                b.DiffsToDelete = diffFiles.Where(f => f.Date != null && f.Date.Value < dtLimit.Value)
                                    .Select(f => f.File)
                                    .ToList();

                                diffFiles = diffFiles.Where(f => f.Date == null || f.Date.Value >= dtLimit.Value)
                                    .ToList();
                            }

                            BlockFileWithTime mostRecentDiff = diffFiles
                                .OrderByDescending(x => x.Date)
                                .FirstOrDefault();

                            if (mostRecentDiff == null || mostRecentDiff.Date < mostRecentFull.Date)
                            {
                                b.BackupMode = BackupMode.Diff;
                                b.DiffDate = mostRecentFull.Date;
                            }
                            else
                            {
                                b.BackupMode = BackupMode.Diff;
                                b.DiffDate = mostRecentDiff.Date;
                            }
                        }
                    }
                }
                bag.Add(new BlockSized { Block = b, Size = size });
            });

            return bag
                .OrderByDescending(i => i.Size)
                .Select(i => i.Block)
                .ToList();
        }

        private class BlockSized
        {
            public CompressionBlock Block { get; set; }
            public long Size { get; set; }
        }

        private class BlockFileWithTime
        {
            public FileInfo File { get; set; }
            public DateTime? Date { get; set; }
        }


        private static DateTime? ParseDateTime(string str)
        {
            // ex : XXXXX_20210318212002.tarzstdaes
            var dateIndex = str.LastIndexOf('_') + 1;
            if (dateIndex <= 1 || str.Length < dateIndex + 14)
            {
                return null;
            }
            var span = str.AsSpan(dateIndex, 14);
            var t = span.ToString();
            if (DateTime.TryParseExact(span, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                return date;
            return null;
        }

        private static bool IsValidDate(string name)
        {
            if (name == null || name.Length != 10)
            {
                return false;
            }
            bool isValid = char.IsDigit(name[0]);
            isValid &= char.IsDigit(name[1]);
            isValid &= char.IsDigit(name[2]);
            isValid &= char.IsDigit(name[3]);
            isValid &= name[4] == '-';
            isValid &= char.IsDigit(name[5]);
            isValid &= char.IsDigit(name[6]);
            isValid &= name[7] == '-';
            isValid &= char.IsDigit(name[8]);
            isValid &= char.IsDigit(name[9]);
            return isValid;
        }
    }
}