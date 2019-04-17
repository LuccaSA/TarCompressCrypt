using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using TCC.Lib.Helpers;

namespace TCC.Lib.Database
{
    public class DesignTccDbContext : IDesignTimeDbContextFactory<TccDbContext>
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
}