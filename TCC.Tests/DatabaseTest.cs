using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCC.Lib.Database;
using TCC.Lib.Helpers;
using Xunit;

namespace TCC.Tests
{
    public class DatabaseTest
    {
        private readonly DatabaseSetup _db;
        private TccBackupDbContext _backupDbContext;

        public DatabaseTest()
        {
            var services = new ServiceCollection();
            services.AddTcc();
            services.PostConfigure<TccSettings>(i =>
            {
                i.BackupConnectionString = "Data Source=:memory:";
                i.RestoreConnectionString = "Data Source=:memory:";
                i.Provider = Provider.SqLite;
            });
            var provider = services.BuildServiceProvider();
            _db = provider.GetRequiredService<DatabaseSetup>();
            _backupDbContext = provider.GetRequiredService<TccBackupDbContext>();
        }

        [Fact]
        public async Task SimpleCrud()
        {
            await _db.EnsureDatabaseExistsAsync(Lib.Options.Mode.Compress | Lib.Options.Mode.Decompress);
            _backupDbContext.BackupJobs.Add(new BackupJob
            {
                StartTime = DateTime.UtcNow,
                Duration = TimeSpan.FromMinutes(2),
                BlockJobs = new List<BackupBlockJob>
                {
                    new BackupBlockJob{ Size = 42, Duration = TimeSpan.FromMinutes(1), BackupSource = new BackupSource{ FullSourcePath = "one"}, Success = true},
                    new BackupBlockJob{ Size = 42, Duration = TimeSpan.FromMinutes(1), BackupSource = new BackupSource{ FullSourcePath = "two"}, Success = true}
                }
            });
            await _backupDbContext.SaveChangesAsync();
            var found = await _backupDbContext.BackupJobs.LastOrDefaultAsync();
            Assert.NotNull(found);
            Assert.Equal(2, found.BlockJobs.Count);
        }
         
    }
}
