using Microsoft.EntityFrameworkCore;

namespace TCC.Lib.Database
{
    public class TccBackupDbContext : DbContext
    {
        public TccBackupDbContext(DbContextOptions options) : base(options) { }

        public DbSet<BackupBlockJob> BackupBlockJobs { get; set; }
        public DbSet<BackupJob> BackupJobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BackupJob>()
                .HasMany(i => i.BlockJobs)
                .WithOne()
                .HasForeignKey(i => i.JobId)
                .IsRequired();

            modelBuilder.Entity<BackupBlockJob>()
                .HasIndex(i => i.StartTime);
            
            base.OnModelCreating(modelBuilder);
        }
    }

    public class TccRestoreDbContext : DbContext
    {
        public TccRestoreDbContext(DbContextOptions options) : base(options) { }

        public DbSet<RestoreBlockJob> RestoreBlockJobs { get; set; }
        public DbSet<RestoreJob> RestoreJobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RestoreJob>()
                .HasMany(i => i.BlockJobs)
                .WithOne()
                .HasForeignKey(i => i.JobId)
                .IsRequired();

            modelBuilder.Entity<RestoreBlockJob>()
                .HasIndex(i => i.StartTime);

            base.OnModelCreating(modelBuilder);
        }
    }

}