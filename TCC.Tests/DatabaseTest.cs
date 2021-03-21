using System;
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
        private readonly TccRestoreDbContext _dbContext;

        public DatabaseTest()
        {
            var services = new ServiceCollection();
            services.AddTcc();
            services.PostConfigure<TccSettings>(i =>
            {
                i.RestoreConnectionString = "Data Source=:memory:";
                i.Provider = Provider.SqLite;
            });
            var provider = services.BuildServiceProvider();
            _db = provider.GetRequiredService<DatabaseSetup>();
            _dbContext = provider.GetRequiredService<TccRestoreDbContext>();
        }

        [Fact]
        public async Task SimpleCrud()
        {
            var dt = DateTime.UtcNow;
            await _db.EnsureDatabaseExistsAsync(Lib.Options.Mode.Compress | Lib.Options.Mode.Decompress);
            _dbContext.RestoreJobs.Add(new RestoreJob
            {
                StartTime = dt,
                Duration = TimeSpan.FromMinutes(2),
            });
            await _dbContext.SaveChangesAsync();
            var found = await _dbContext.RestoreJobs.FirstOrDefaultAsync();
            Assert.NotNull(found);
            Assert.Equal(dt, found.StartTime);
        }
    }
}
