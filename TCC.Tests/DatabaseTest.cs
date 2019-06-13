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
        private readonly Database _db;
        
        public DatabaseTest()
        {
            var services = new ServiceCollection();
            services.AddTcc();
            services.PostConfigure<TccSettings>(i =>
            {
                //i.ConnectionString = "Data Source=:memory:";
                i.Provider = Provider.InMemory;
            });
            var provider = services.BuildServiceProvider();
            _db = provider.GetRequiredService<Database>();
        }

        [Fact]
        public async Task SimpleCrud()
        {
            var db = await _db.GetDbAsync();
            await db.Database.EnsureCreatedAsync();

            db.BackupJobs.Add(new BackupJob
            {
                StartTime = DateTime.UtcNow,
                Duration = TimeSpan.FromMinutes(2),
                BlockJobs = new List<BackupBlockJob>
                {
                    new BackupBlockJob{ Size = 42, Duration = TimeSpan.FromMinutes(1), FullSourcePath = "one", Success = true},
                    new BackupBlockJob{ Size = 42, Duration = TimeSpan.FromMinutes(1), FullSourcePath = "two", Success = true}
                }
            });
            await db.SaveChangesAsync();
            var found = await db.BackupJobs.LastOrDefaultAsync();
            Assert.NotNull(found);
            Assert.Equal(2, found.BlockJobs.Count);
        }
         
    }
}
