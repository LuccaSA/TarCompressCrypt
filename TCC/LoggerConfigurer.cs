using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Json;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using TCC.SerilogAsync;

namespace TCC
{
    public static class LoggerConfigurer
    {
        public const string AuditScope = "audit";

        private static readonly Option<string> _auditOption = new Option<string>(new[] { "--auditFile" }, "Path to the audit log file");
        private static readonly Option<bool> _verboseOption = new Option<bool>(new[] { "-v", "--verbose" }, "Verbose output");
        private static readonly Option<string> _logPath = new Option<string>(new[] { "--logPath" }, "Log path");

        public static IServiceCollection AddTccLogger(this IServiceCollection services)
        {
            services.AddSingleton<ILoggerProvider>(sp =>
            {
                var context = sp.GetRequiredService<InvocationContext>();

                var auditPath = context.ParseResult.GetValueForOption(_auditOption);
                var verbose = context.ParseResult.GetValueForOption(_verboseOption);
                var logPath = context.ParseResult.GetValueForOption(_logPath);

                var logger = CreateLogger(verbose, logPath, auditPath);

                return new SerilogLoggerProvider(logger);
            });
            return services;
        }

        public static CommandLineBuilder AddTccLoggerOptions(this CommandLineBuilder commandLineBuilder)
        {
            commandLineBuilder.Command.AddGlobalOption(_auditOption);
            commandLineBuilder.Command.AddGlobalOption(_verboseOption);
            commandLineBuilder.Command.AddGlobalOption(_logPath);
            return commandLineBuilder;
        }

        private static Logger CreateLogger(bool verbose, string logDirectoryPath, string auditFile)
        {
            string path = null;
            if (logDirectoryPath != null && Directory.Exists(logDirectoryPath))
            {
                path = logDirectoryPath;
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(logDirectoryPath);
                }
                catch (Exception)
                {
                    path = "logs";
                }
            }

            var level = verbose ? LogEventLevel.Debug : LogEventLevel.Information;
            var loggerConf = new LoggerConfiguration()
                .WriteTo.Logger(logger => logger
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                    .WriteTo.Async(conf =>
                    {
                        if (path is not null)
                        {
                            conf.File(Path.Combine(path, "tcc.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31);
                        }
                        conf.Console();
                    })
                   .Filter.ByExcluding(IsAuditLog)
                   .MinimumLevel.Is(level)
                );
            if (!string.IsNullOrEmpty(auditFile))
            {
                if (!Path.IsPathRooted(auditFile) && path is not null)
                {
                    auditFile = Path.Combine(path, auditFile);
                }
                loggerConf
                    .WriteTo.Logger(auditLogger =>
                    {
                        auditLogger
                            .MinimumLevel.Information()
                            .WriteTo.Async(conf =>
                            {
                                conf.File(new AuditLogJsonFormatter(), auditFile, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31);
                            })
                            .Filter.ByIncludingOnly(IsAuditLog);
                    });
            }

            return loggerConf.CreateLogger();
        }

        private static bool IsAuditLog(LogEvent logEvent)
        {
            LogEventPropertyValue scope = logEvent.Properties.GetValueOrDefault("Scope");
            if (scope is null)
            {
                return false;
            }
            return
                scope is SequenceValue sequenceValue &&
                sequenceValue
                    .Elements
                    .OfType<ScalarValue>()
                    .Any(scalarValue => string.Equals(scalarValue.Value as string, AuditScope));
        }

    }
}
