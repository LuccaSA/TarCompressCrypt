using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TCC.Lib.Benchmark;
using TCC.Lib.Blocks;
using TCC.Lib.Command;
using TCC.Lib.Database;
using TCC.Lib.Dependencies;
using TCC.Lib.Notification;

namespace TCC.Lib.Helpers
{
    public static class TccRegisteringExtensions
    {
        public static void AddTcc(this IServiceCollection services, string workingPath = null)
        {
            services.AddScoped<ExternalDependencies>();
            services.AddScoped<TarCompressCrypt>();
            services.AddScoped<EncryptionCommands>();
            services.AddScoped<CompressionCommands>();
            services.AddScoped<BenchmarkRunner>();
            services.AddScoped<BenchmarkOptionHelper>();
            services.AddScoped<BenchmarkIterationGenerator>();
            services.AddScoped(_ => new CancellationTokenSource());
            services.AddScoped<DatabaseSetup>();
            services.AddScoped<DatabaseHelper>();

            services.AddScoped<SlackSender>();
            services.AddScoped<SlackClient>();

            services.RegisterDbContext<TccRestoreDbContext>(workingPath);
        }

        private static void RegisterDbContext<TDbContext>(this IServiceCollection services, string workingPath)
            where TDbContext : DbContext
        {
            services.AddDbContextPool<TDbContext>((s, options) =>
            {
                var setting = s.GetRequiredService<IOptions<TccSettings>>().Value;
                try
                {
                    switch (setting.Provider)
                    {
                        case Provider.SqlServer:
                        {
                            var cs = setting.RestoreConnectionString;
                            options.UseSqlServer(cs);
                            break;
                        }
                        case Provider.SqLite:
                        {
                            string cs = GetSqLiteConnectionString(setting.RestoreConnectionString, workingPath);
                            var sqLite = new SqliteConnection(cs);
                            sqLite.Open();
                            options.UseSqlite(sqLite);
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception e)
                {
                    throw new TccException($"{setting.Provider} : Error connecting on {setting.RestoreConnectionString}", e);
                }
            });
        }

        private static string GetSqLiteConnectionString(string settingConnectionString, string workingPath)
        {
            if (string.IsNullOrWhiteSpace(settingConnectionString) && !string.IsNullOrWhiteSpace(workingPath))
            {
                // default SqLite path on source & destination targets
                if (!Path.IsPathRooted(workingPath))
                {
                    throw new ArgumentException($"Path {workingPath} isn't absolute");
                }

                var dir = new DirectoryInfo(workingPath);
                return "Data Source=" + Path.Combine(dir.FullName, "tcc.db");
            }
            // fallback
            return string.IsNullOrWhiteSpace(settingConnectionString) ? "Data Source=tcc.db" : settingConnectionString;
        }
    }

    public class TccSettings
    {
        public string RestoreConnectionString { get; set; }
        public Provider Provider { get; set; } = Provider.SqLite;
    }

    [DefaultValue(SqLite)]
    public enum Provider
    {
        SqLite,
        SqlServer
    }
}
