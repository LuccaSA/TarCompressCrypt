using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCC.Lib.Benchmark;
using TCC.Lib.Blocks;
using TCC.Lib.Dependencies;

namespace TCC.Lib.Helpers
{
    public static class TccRegisteringExtensions
    {
        public static void AddTcc(this IServiceCollection services)
        {
            services.Configure<TccSettings>(i => { i.ConnectionString = "Data Source=tcc.db"; });
            services.TryAddScoped<IBlockListener, GenericBlockListener>();
            services.TryAddScoped(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddScoped<ExternalDependencies>();
            services.AddScoped<TarCompressCrypt>();
            services.AddScoped<EncryptionCommands>();
            services.AddScoped<CompressionCommands>();
            services.AddScoped<BenchmarkRunner>();
            services.AddScoped<BenchmarkOptionHelper>();
            services.AddScoped<BenchmarkIterationGenerator>();
            services.AddScoped(_ => new CancellationTokenSource());

            services.AddDbContext<TccDbContext>((s,options) =>
            {
                var cs = s.GetRequiredService<IOptions<TccSettings>>().Value.ConnectionString;
                var sqlite = new SqliteConnection(cs);
                sqlite.Open();
                options.UseSqlite(sqlite);
            });
        }
    }

    public class TccSettings
    {
        public string ConnectionString { get; set; }
    }
}
