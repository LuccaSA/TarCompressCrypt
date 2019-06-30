using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.AspNetCore;

namespace TCC
{
    public static class LoggingExtensions
    {
        public static void AddSerilog(this IServiceCollection serviceCollection, bool optionVerbose, string workingPath)
        {
            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<ILoggerFactory>(servicesProvider =>
            {
                var loggerConfiguration = new LoggerConfiguration();
                if (optionVerbose)
                {
                    loggerConfiguration.WriteTo.Console();
                }
                string path = string.IsNullOrWhiteSpace(workingPath) ? "logs/tcc.log" : Path.Combine(workingPath, "tcc.log");
                loggerConfiguration.WriteTo.File(path);
                var logger = loggerConfiguration.CreateLogger();
                return new SerilogLoggerFactory(logger, true);
            });
        }
    }
}