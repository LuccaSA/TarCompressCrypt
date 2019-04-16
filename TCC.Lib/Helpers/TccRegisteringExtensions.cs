using System;
using System.ComponentModel;
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
                var setting = s.GetRequiredService<IOptions<TccSettings>>().Value;
                switch (setting.Provider)
                {
                    case Provider.InMemory:
                        options.UseInMemoryDatabase(Guid.NewGuid().ToString());
                        break;
                    case Provider.SqlServer:
                        options.UseSqlServer(setting.ConnectionString);
                        break;
                    case Provider.SqLite:
                        var sqlite = new SqliteConnection(setting.ConnectionString);
                        sqlite.Open();
                        options.UseSqlite(sqlite);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });
        }
    }

    public class TccSettings
    {
        public string ConnectionString { get; set; }
        public Provider Provider { get; set; } = Provider.SqLite;
    }

    [DefaultValue(SqLite)]
    public enum Provider
    {
        SqLite,
        InMemory,
        SqlServer
    }
}
