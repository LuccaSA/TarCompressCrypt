using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace TCC.Lib.Helpers
{
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
        public BlockBackupType BackupType { get; set; }
        public String Source { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public long Size { get; set; }
        public bool Success { get; set; }
        public string Exception { get; set; }
    }

    public enum BlockBackupType
    {
        Full,
        Diff
    }

}