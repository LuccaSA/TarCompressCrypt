using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using TCC.Lib.Helpers;

namespace TCC.Lib.Database
{
    public class DesignTccBackupDbContext : IDesignTimeDbContextFactory<TccBackupDbContext>
    {
        public TccBackupDbContext CreateDbContext(string[] args)
        {
            var services = new ServiceCollection();
            services.AddTcc();
            services.PostConfigure<TccSettings>(i =>
            {
                i.BackupConnectionString = "Data Source=:memory:";
            });
            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<TccBackupDbContext>();
        }
    }

    public class DesignTccRestoreDbContext : IDesignTimeDbContextFactory<TccRestoreDbContext>
    {
        public TccRestoreDbContext CreateDbContext(string[] args)
        {
            var services = new ServiceCollection();
            services.AddTcc();
            services.PostConfigure<TccSettings>(i =>
            {
                i.RestoreConnectionString = "Data Source=:memory:";
            });
            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<TccRestoreDbContext>();
        }
    }
}