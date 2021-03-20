using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TCC.Lib.Options;

namespace TCC.Lib.Database
{
    public class DatabaseSetup
    {
        private readonly TccRestoreDbContext _tccRestoreDbContext;

        public DatabaseSetup( TccRestoreDbContext tccRestoreDbContext)
        {
            _tccRestoreDbContext = tccRestoreDbContext;
        }

        public async Task EnsureDatabaseExistsAsync(Mode commandMode)
        {
            if (commandMode.HasFlag(Mode.Decompress))
            {
                if ((await _tccRestoreDbContext.Database.GetPendingMigrationsAsync()).Any())
                {
                    await _tccRestoreDbContext.Database.MigrateAsync();
                }
            }
        }

        public async Task CleanupDatabaseAsync(Mode commandMode)
        {
            await Task.CompletedTask;

            if (commandMode.HasFlag(Mode.Decompress))
            {
                // Remove Full and Diff older than the last Full
                var paths = await _tccRestoreDbContext.RestoreDestinations
                    .ToListAsync();

                foreach (var p in paths)
                {
                    var lastFull = await _tccRestoreDbContext.RestoreBlockJobs
                        .Where(i => i.RestoreDestination.Id == p.Id && i.BackupMode == BackupMode.Full)
                        .OrderByDescending(i => i.StartTime)
                        .FirstOrDefaultAsync();

                    if (lastFull != null)
                    {
                        var toDelete = await _tccRestoreDbContext.RestoreBlockJobs
                            .Where(i => i.RestoreDestination.Id == p.Id && i.StartTime < lastFull.StartTime)
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