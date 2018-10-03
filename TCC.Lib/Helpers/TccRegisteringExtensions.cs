using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using TCC.Lib.Benchmark;
using TCC.Lib.Dependencies;

namespace TCC.Lib.Helpers
{
    public static class TccRegisteringExtensions
    {
        public static void AddTcc(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<BlockListener>();
            serviceCollection.AddSingleton<ExternalDependencies>();
            serviceCollection.AddSingleton<TarCompressCrypt>();
            serviceCollection.AddSingleton<BenchmarkHelper>();
            serviceCollection.AddSingleton(_ => new CancellationTokenSource());
        }
    }
}
