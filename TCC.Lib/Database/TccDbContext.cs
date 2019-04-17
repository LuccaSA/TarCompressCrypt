using Microsoft.EntityFrameworkCore;

namespace TCC.Lib.Database
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
}