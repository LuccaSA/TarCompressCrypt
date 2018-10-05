using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.AspNetCore;

namespace TCC
{
    public static class LoggingExtensions
    {
        public static void AddSerilog(this IServiceCollection serviceCollection, bool optionVerbose)
        {
            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<ILoggerFactory>(servicesProvider =>
            {
                var loggerConfiguration = new LoggerConfiguration();
                if (optionVerbose)
                {
                    loggerConfiguration.WriteTo.Console();
                }
                loggerConfiguration.WriteTo.File("logs/tcc.log");
                var logger = loggerConfiguration.CreateLogger();
                return new SerilogLoggerFactory(logger, true);
            });
        }
    }
}