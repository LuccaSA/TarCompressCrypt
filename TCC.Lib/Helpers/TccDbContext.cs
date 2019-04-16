using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;

namespace TCC.Lib.Helpers
{
    public class Database
    {  
        private readonly AsyncLazy<TccDbContext> _lazy;

        public Database(TccDbContext tccDbContext, IOptions<TccSettings> options)
        {  
            _lazy = new AsyncLazy<TccDbContext>(async () =>
            {
                if (options.Value.Provider == Provider.InMemory)
                {
                    await tccDbContext.Database.EnsureCreatedAsync();
                }
                else
                {
                    await tccDbContext.Database.MigrateAsync();
                }
                return tccDbContext;
            });
        }

        public async Task<TccDbContext> GetDbAsync() => await _lazy;
    }

    public class TccDbContext : DbContext
    {
        public TccDbContext(DbContextOptions options) : base(options) { }

        public DbSet<BlockJob> BlockJobs { get; set; }
        public DbSet<Job> Jobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Job>().HasMany(i => i.BlockJobs).WithOne().HasForeignKey(i => i.JobId).IsRequired();
            modelBuilder.Entity<BlockJob>().HasIndex(i => i.StartTime);
            
            base.OnModelCreating(modelBuilder);
        }
    }

    public class DesingTccDbContext : IDesignTimeDbContextFactory<TccDbContext>
    {
        public TccDbContext CreateDbContext(string[] args)
        {
            var services = new ServiceCollection();
            services.AddTcc();
            services.PostConfigure<TccSettings>(i =>
            {
                i.ConnectionString = "Data Source=:memory:";
            });
            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<TccDbContext>();
        }
    }

    public class Job
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public List<BlockJob> BlockJobs { get; set; }
    }

    public class BlockJob
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public Job Job { get; set; }
        public BackupMode BackupMode { get; set; }
        public String Source { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public long Size { get; set; }
        public bool Success { get; set; }
        public string Exception { get; set; }
    }

    [DefaultValue(Diff)]
    public enum BackupMode
    {
        Diff,
        Full,
    }

}