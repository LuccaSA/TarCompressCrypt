using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TCC.Lib.Database;

namespace TCC.Lib
{
    public class DatabaseHelper
    {
        private readonly ILogger<DatabaseHelper> _logger;
        private readonly IServiceProvider _serviceProvider;

        public DatabaseHelper(ILogger<DatabaseHelper> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }
        
        public async Task<RestoreJob> InitializeRestoreJobAsync()
        {
            var job = new RestoreJob
            {
                StartTime = DateTime.UtcNow,
                BlockJobs = new List<RestoreBlockJob>()
            };
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TccRestoreDbContext>();
                db.RestoreJobs.Add(job);
                await db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                _logger.LogCritical("InitializeRestoreJobAsync", e);
            }
            return job;
        }

        public async Task UpdateRestoreJobStatsAsync(Stopwatch sw, RestoreJob job)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TccRestoreDbContext>();
                var jobToUpdate = await db.RestoreJobs.FirstOrDefaultAsync(i => i.Id == job.Id);
                jobToUpdate.Duration = sw.Elapsed;
                await db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                _logger.LogCritical("UpdateRestoreJobStatsAsync", e);
            }
        }

        public async Task AddRestoreBlockJobAsync(RestoreJob job, IReadOnlyCollection<OperationDecompressionsBlock> blocks)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TccRestoreDbContext>();

                var theJob = await db.RestoreJobs.Include(i => i.BlockJobs).FirstOrDefaultAsync(i => i.Id == job.Id);

                foreach (OperationDecompressionsBlock odb in blocks)
                {
                    var thePath = await db.RestoreDestinations.FirstOrDefaultAsync(i => i.FullDestinationPath == odb.Batch.DestinationFolder);
                    if (thePath == null)
                    {
                        thePath = new RestoreDestination { FullDestinationPath = odb.Batch.DestinationFolder };
                    }

                    if (odb.Batch.BackupFull != null)
                    {
                        var rb = new RestoreBlockJob
                        {
                            Job = job,
                            StartTime = odb.Batch.BackupFull.BlockDateTime,
                            RestoreDestination = thePath,
                            Duration = TimeSpan.FromMilliseconds(odb.Batch.BackupFullCommandResult.ElapsedMilliseconds),
                            Size = odb.Batch.BackupFull.CompressedSize,
                            Exception = odb.Batch.BackupFullCommandResult.Errors,
                            Success = odb.Batch.BackupFullCommandResult.IsSuccess,
                            BackupMode = BackupMode.Full
                        };
                        theJob.BlockJobs.Add(rb);
                    }

                    if (odb.Batch.BackupsDiff != null)
                    {
                        for (var index = 0; index < odb.Batch.BackupsDiff.Length; index++)
                        {
                            var b = odb.Batch.BackupsDiff[index];
                            var rb = new RestoreBlockJob
                            {
                                Job = job,
                                StartTime = b.BlockDateTime,
                                RestoreDestination = thePath,
                                Duration = TimeSpan.FromMilliseconds(odb.Batch.BackupDiffCommandResult[index].ElapsedMilliseconds),
                                Size = b.CompressedSize,
                                Exception = odb.Batch.BackupDiffCommandResult[index].Errors,
                                Success = odb.Batch.BackupDiffCommandResult[index].IsSuccess,
                                BackupMode = BackupMode.Diff
                            };
                            theJob.BlockJobs.Add(rb);
                        }
                    }
                }
                await db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                _logger.LogCritical("AddRestoreBlockJobAsync", e);
            }
        }

    }
}