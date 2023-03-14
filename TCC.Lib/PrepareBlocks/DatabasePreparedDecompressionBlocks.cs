using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TCC.Lib.Blocks;
using TCC.Lib.Database;

namespace TCC.Lib.PrepareBlocks
{
    public class DatabasePreparedDecompressionBlocks : IPrepareDecompressBlocks
    {
        private readonly TccRestoreDbContext _restoreDb;
        private readonly ILogger<TarCompressCrypt> _logger;

        public DatabasePreparedDecompressionBlocks(TccRestoreDbContext restoreDb, ILogger<TarCompressCrypt> logger)
        {
            _logger = logger;
            _restoreDb = restoreDb;
        }

        public async IAsyncEnumerable<DecompressionBatch> PrepareDecompressionBlocksAsync(IEnumerable<DecompressionBatch> blocks)
        {
            foreach (DecompressionBatch decompBlock in blocks.OrderByDescending(i => i.CompressedSize))
            {
                var opFolder = decompBlock.DestinationFolder;

                // If target directory doesn't exists, then we restore FULL + DIFF
                var dir = new DirectoryInfo(decompBlock.DestinationFolder);
                if (!dir.Exists || !dir.EnumerateDirectories().Any() && !dir.EnumerateFiles().Any())
                {
                    yield return decompBlock;
                    continue;
                }

                var lastRestore = await _restoreDb.RestoreBlockJobs
                    .Where(i => i.RestoreDestination.FullDestinationPath == opFolder)
                    .OrderByDescending(i => i.StartTime)
                    .FirstOrDefaultAsync();

                if (lastRestore == null)
                {
                    if (decompBlock.BackupFull == null)
                    {
                        throw new Exception("missing backup full");
                    }

                    // no backup full in history : we decompress the FULL + DIFF
                    yield return decompBlock;
                    continue;
                }

                var d = new DecompressionBatch();

                DateTime recent = lastRestore.StartTime;
                // if more recent full, we take it
                if (decompBlock.BackupFull != null)
                {
                    if (decompBlock.BackupFull.BackupDate > recent)
                    {
                        recent = decompBlock.BackupFull.BackupDate.Value;
                        // if no diff, check more recent full
                        d.BackupFull = decompBlock.BackupFull;
                    }
                }

                // we yield all the DIFF archives more recent the the last restore or the last full
                d.BackupsDiff = decompBlock.BackupsDiff?.Where(i => i.BackupDate > recent).ToArray();

                if (d.BackupFull != null || d.BackupsDiff?.Length > 0)
                {
                    yield return d;
                }
            }

            // TODO next
            // - si on déclenche un restore d'un FULL sur un dossier ou y'a deja des données, faut cleaner avant
        }
    }
}