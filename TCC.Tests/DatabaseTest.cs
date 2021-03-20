using System;
using System.Collections.Generic;
using System.Linq;
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
        private TccRestoreDbContext _dbContext;

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
            await _db.EnsureDatabaseExistsAsync(Lib.Options.Mode.Compress | Lib.Options.Mode.Decompress);
            _dbContext.RestoreBlockJobs.Add(new RestoreBlockJob()
            {
                StartTime = DateTime.UtcNow,
                Duration = TimeSpan.FromMinutes(2),
            });
            await _dbContext.SaveChangesAsync();
            var found = await _dbContext.RestoreBlockJobs.OrderByDescending(i=>i.Id).FirstOrDefaultAsync();
            Assert.NotNull(found);
        }
    }
}
