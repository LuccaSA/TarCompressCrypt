using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using TCC.Lib;
using TCC.Lib.Benchmark;
using TCC.Lib.Command;
using TCC.Lib.Database;
using TCC.Lib.Dependencies;
using TCC.Lib.Notification;

namespace TCC
{
    public static class TccRegisteringExtensions
    {
        public static void AddTcc(this IServiceCollection services)
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

            services.RegisterDbContext<TccRestoreDbContext>();
        }

        private static void RegisterDbContext<TDbContext>(this IServiceCollection services)
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
                            var context = s.GetService<InvocationContext>();
                            string workingPath = null;
                            if (context is not null)
                            {
                                workingPath = context
                                    .ParseResult
                                    .CommandResult
                                    .Children
                                    .OfType<ArgumentResult>()
                                    .FirstOrDefault()
                                    ?.Tokens
                                    ?.FirstOrDefault()
                                    .Value;
                            }
                            if (workingPath is not null && !Directory.Exists(workingPath))
                            {
                                workingPath = new FileInfo(workingPath).Directory?.FullName;
                            }
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
