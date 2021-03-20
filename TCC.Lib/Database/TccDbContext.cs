using Microsoft.EntityFrameworkCore;

namespace TCC.Lib.Database
{
    public class TccRestoreDbContext : DbContext
    {
        public TccRestoreDbContext(DbContextOptions<TccRestoreDbContext> options) : base(options) { }

        public DbSet<RestoreBlockJob> RestoreBlockJobs { get; set; }
        public DbSet<RestoreJob> RestoreJobs { get; set; }
        public DbSet<RestoreDestination> RestoreDestinations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RestoreJob>()
                .HasMany(i => i.BlockJobs)
                .WithOne(i => i.Job)
                .IsRequired();

            modelBuilder.Entity<RestoreDestination>()
                .HasMany(i => i.RestoreBlockJobs)
                .WithOne(i => i.RestoreDestination)
                .IsRequired();

            modelBuilder.Entity<RestoreBlockJob>().HasIndex(i => i.StartTime);
            modelBuilder.Entity<RestoreDestination>().HasIndex(p => p.FullDestinationPath).IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }

}