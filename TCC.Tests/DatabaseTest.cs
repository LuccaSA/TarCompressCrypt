using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCC.Lib.Helpers;
using Xunit;

namespace TCC.Tests
{
    public class DatabaseTest
    {
        private readonly TccDbContext _tccDbContext;

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
            _tccDbContext = provider.GetRequiredService<TccDbContext>();
        }

        [Fact]
        public async Task SimpleCrud()
        {
            await _tccDbContext.Database.EnsureCreatedAsync(); 

            _tccDbContext.Jobs.Add(new Job
            {
                StartTime = DateTime.UtcNow,
                Duration = TimeSpan.FromMinutes(2),
                BlockJobs = new List<BlockJob>
                {
                    new BlockJob{ Size = 42, Duration = TimeSpan.FromMinutes(1), Source = "one", Success = true},
                    new BlockJob{ Size = 42, Duration = TimeSpan.FromMinutes(1), Source = "two", Success = true}
                }
            });
            await _tccDbContext.SaveChangesAsync();
            var found = await _tccDbContext.Jobs.LastOrDefaultAsync();
            Assert.NotNull(found);
            Assert.Equal(2, found.BlockJobs.Count);
        }
         
    }
}
