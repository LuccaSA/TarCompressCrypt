using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using TCC.Lib.Blocks;
using TCC.Lib.Database;
using TCC.Lib.Helpers;

namespace TCC.Lib.PrepareBlocks
{
    public class DatabasePreparedCompressionBlocks : IPrepareCompressBlocks
    {
        private readonly TccBackupDbContext _backupDb;
        private readonly ILogger<TarCompressCrypt> _logger;
        private readonly BackupMode? _backupMode;

        public DatabasePreparedCompressionBlocks(TccBackupDbContext db, BackupMode? backupMode, ILogger<TarCompressCrypt> logger)
        {
            _backupDb = db;
            _logger = logger;
            _backupMode = backupMode;
        }

        public async IAsyncEnumerable<CompressionBlock> PrepareCompressionBlocksAsync(IEnumerable<CompressionBlock> blocks)
        {
            var lastFulls = await _backupDb.BackupBlockJobs
                .Include(i => i.BackupSource)
                .Where(j => j.BackupMode == BackupMode.Full)
                .ToListAsync();

            // order by size of bigger backup full to optimize global time
            lastFulls = lastFulls
                .GroupBy(i => i.BackupSource.FullSourcePath)
                .Select(i => i.OrderByDescending(b => b.Size).FirstOrDefault())
                .OrderByDescending(i => i.Size)
                .ToList();

            if (lastFulls.Count == 0)
            {
                // no history ATM, we consider a backup full for each block
                _logger.LogInformation("No backup history, processing files in filesystem order");
                foreach (var b in blocks)
                {
                    b.BackupMode = BackupMode.Full;
                    yield return b;
                }
                yield break;
            }

            await foreach (var b in blocks.OrderBySequence(lastFulls,
                b => b.SourceFileOrDirectory.FullPath,
                p => p.BackupSource.FullSourcePath,
                async (b, p) =>
                {
                    b.LastBackupSize = p.Size;

                    if (_backupMode == BackupMode.Full)
                    {
                        // If Full here, it's a request from command line
                        return; // we respect the command line
                    }

                    var lastBackup = await Queryable.Where(_backupDb.BackupBlockJobs, i => i.BackupSource.FullSourcePath == b.SourceFileOrDirectory.FullPath)
                        .OrderByDescending(i => i.StartTime)
                        .FirstOrDefaultAsync();

                    if (lastBackup != null && string.IsNullOrEmpty(lastBackup.Exception) && b.HaveFullFiles)
                    {
                        // If last backup found, we plan a backup diff
                        b.BackupMode = BackupMode.Diff;
                        b.DiffDate = lastBackup.StartTime;
                    }
                    else
                    {
                        // No previous backup, we start with a full
                        b.BackupMode = BackupMode.Full;
                    }
                }))
            {
                yield return b;
            }
        }
    }
}