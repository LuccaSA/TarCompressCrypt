using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Database
{
    public class DatabaseSetup
    {
        private readonly TccBackupDbContext _tccBackupDbContext;
        private readonly TccRestoreDbContext _tccRestoreDbContext;
        private readonly IOptions<TccSettings> _options;

        public DatabaseSetup(TccBackupDbContext tccBackupDbContext, TccRestoreDbContext tccRestoreDbContext, IOptions<TccSettings> options)
        {
            _tccBackupDbContext = tccBackupDbContext;
            _tccRestoreDbContext = tccRestoreDbContext;
            _options = options;
        }

        public async Task EnsureDatabaseExistsAsync(Mode commandMode)
        {
            if (_options.Value.Provider == Provider.InMemory)
            {
                if (commandMode.HasFlag(Mode.Compress))
                {
                    await _tccBackupDbContext.Database.EnsureCreatedAsync();
                }

                if (commandMode.HasFlag(Mode.Decompress))
                {
                    await _tccRestoreDbContext.Database.EnsureCreatedAsync();
                }
            }
            else
            {
                if (commandMode.HasFlag(Mode.Compress))
                {
                    if ((await _tccBackupDbContext.Database.GetPendingMigrationsAsync()).Any())
                    {
                        await _tccBackupDbContext.Database.MigrateAsync();
                    }
                }

                if (commandMode.HasFlag(Mode.Decompress))
                {
                    if ((await _tccRestoreDbContext.Database.GetPendingMigrationsAsync()).Any())
                    {
                        await _tccRestoreDbContext.Database.MigrateAsync();
                    }
                }
            }
        }

        public async Task CleanupDatabaseAsync(Mode commandMode)
        {
            await Task.CompletedTask;

            if (commandMode.HasFlag(Mode.Compress))
            {
                // Remove Full and Diff older than the last Full
                var paths = await _tccBackupDbContext.BackupBlockJobs
                    .Select(i => i.FullSourcePath)
                    .Distinct()
                    .ToListAsync();

                foreach (var p in paths)
                {
                    var lastFull = await _tccBackupDbContext.BackupBlockJobs
                                            .Where(i => i.FullSourcePath == p && i.BackupMode == BackupMode.Full)
                                            .OrderByDescending(i => i.StartTime)
                                            .FirstOrDefaultAsync();
                    if (lastFull != null)
                    {
                        var toDelete = await _tccBackupDbContext.BackupBlockJobs
                            .Where(i => i.FullSourcePath == p && i.StartTime < lastFull.StartTime)
                            .ToListAsync();
                        if (toDelete.Count != 0)
                        {
                            _tccBackupDbContext.BackupBlockJobs.RemoveRange(toDelete);
                        }
                    }
                }

                await _tccBackupDbContext.SaveChangesAsync();

                // Remove empty jobs
                var jobsToDelete = await _tccBackupDbContext.BackupJobs.Where(i => !i.BlockJobs.Any()).ToListAsync();
                if (jobsToDelete.Any())
                {
                    _tccBackupDbContext.BackupJobs.RemoveRange(jobsToDelete);

                    await _tccBackupDbContext.SaveChangesAsync();
                }
            }

            if (commandMode.HasFlag(Mode.Decompress))
            {
                // Remove Full and Diff older than the last Full
                var paths = await _tccRestoreDbContext.RestoreBlockJobs
                    .Select(i => i.FullDestinationPath)
                    .Distinct()
                    .ToListAsync();

                foreach (var p in paths)
                {
                    var lastFull = await _tccRestoreDbContext.RestoreBlockJobs
                        .Where(i => i.FullDestinationPath == p && i.BackupMode == BackupMode.Full)
                        .OrderByDescending(i => i.StartTime)
                        .FirstOrDefaultAsync();

                    if (lastFull != null)
                    {
                        var toDelete = await _tccRestoreDbContext.RestoreBlockJobs
                            .Where(i => i.FullDestinationPath == p && i.StartTime < lastFull.StartTime)
                            .ToListAsync();
                        if (toDelete.Count != 0)
                        {
                            _tccRestoreDbContext.RestoreBlockJobs.RemoveRange(toDelete);
                        }
                    }
                }

                await _tccRestoreDbContext.SaveChangesAsync();

                // Remove empty jobs
                var jobsToDelete = await _tccRestoreDbContext.RestoreJobs.Where(i => !i.BlockJobs.Any()).ToListAsync();
                if (jobsToDelete.Any())
                {
                    _tccRestoreDbContext.RestoreJobs.RemoveRange(jobsToDelete);

                    await _tccRestoreDbContext.SaveChangesAsync();
                }
            }
        }
    }
}