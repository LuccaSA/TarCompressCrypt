using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

namespace TCC
{
    public static class LoggingExtensions
    {
        public static void AddSerilog(this IServiceCollection serviceCollection, bool optionVerbose, string workingPath)
        {
            serviceCollection.AddLogging(l =>
            {
                var logger = CreateSerilogLogger(optionVerbose, workingPath);
                l.AddSerilog(logger, true);
            });
        }

        private static Serilog.Core.Logger  CreateSerilogLogger(bool optionVerbose, string workingPath)
        {
            string path = string.IsNullOrWhiteSpace(workingPath) ? "logs/tcc.log" : Path.Combine(workingPath, "tcc.log");

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Verbose()
                .WriteTo.File(path);

            if (optionVerbose)
            {
                loggerConfiguration.WriteTo.Console();
            }
             
            return loggerConfiguration.CreateLogger();
        }
    }
}