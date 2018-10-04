using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCC.Lib.Benchmark;
using TCC.Lib.Blocks;
using TCC.Lib.Dependencies;

namespace TCC.Lib.Helpers
{
    public static class TccRegisteringExtensions
    {
        public static void AddTcc(this IServiceCollection serviceCollection)
        {
            serviceCollection.TryAddSingleton<IBlockListener, GenericBlockListener>();
            serviceCollection.AddSingleton<ExternalDependencies>();
            serviceCollection.AddSingleton<TarCompressCrypt>();
            serviceCollection.AddSingleton<BenchmarkHelper>();
            serviceCollection.AddSingleton(_ => new CancellationTokenSource());
        }
    }
}
