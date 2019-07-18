using Microsoft.EntityFrameworkCore;

namespace TCC.Lib.Database
{
    public class TccBackupDbContext : DbContext
    {
        public TccBackupDbContext(DbContextOptions<TccBackupDbContext> options) : base(options) { }

        public DbSet<BackupBlockJob> BackupBlockJobs { get; set; }
        public DbSet<BackupJob> BackupJobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BackupJob>()
                .HasMany(i => i.BlockJobs)
                .WithOne(i => i.Job)
                .IsRequired();

            modelBuilder.Entity<BackupBlockJob>().HasIndex(i => i.StartTime);
            modelBuilder.Entity<BackupBlockJob>().HasIndex(p => new { p.FullSourcePath, p.StartTime });

            base.OnModelCreating(modelBuilder);
        }
    }

    public class TccRestoreDbContext : DbContext
    {
        public TccRestoreDbContext(DbContextOptions<TccRestoreDbContext> options) : base(options) { }

        public DbSet<RestoreBlockJob> RestoreBlockJobs { get; set; }
        public DbSet<RestoreJob> RestoreJobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RestoreJob>()
                .HasMany(i => i.BlockJobs)
                .WithOne(i => i.Job)
                .IsRequired();

            modelBuilder.Entity<RestoreBlockJob>().HasIndex(i => i.StartTime);
            modelBuilder.Entity<RestoreBlockJob>().HasIndex(p => new { p.FullDestinationPath, p.StartTime });

            base.OnModelCreating(modelBuilder);
        }
    }

}