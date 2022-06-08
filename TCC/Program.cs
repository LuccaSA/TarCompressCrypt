using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using System.Threading;
using System.Threading.Tasks;
using TCC.Parser;

namespace TCC
{
    public static class Program
    {
        private const string _tccMutex = "Global\\FD70BFC5-79C8-44DF-9629-65512A1CD0FC";

        static async Task Main(string[] args)
        {
            using var mutex = new Mutex(false, _tccMutex);
            if (!mutex.WaitOne(0, false))
            {
                Console.Error.WriteLine("Tcc is already running");
                return;
            }

            await BuildCommandLine()
                .UseHost(_ => Host.CreateDefaultBuilder(),
                    host =>
                    {
                        host.ConfigureServices(services =>
                        {
                            services.AddTccLogger();
                            services.AddScoped<ITccController, TccController>();
                            services.AddTcc();
                        });
                    })
                .Build()
                .InvokeAsync(args);
        }

        public static CommandLineBuilder BuildCommandLine()
            => new CommandLineBuilder(new RootCommand
                {
                    new AutoDecompressCommand(),
                    new CompressCommand(),
                    new DecompressCommand(),
                    new BenchmarkCommand()
                })
                .AddTccLoggerOptions()
                .UseDefaults();
    }
}
