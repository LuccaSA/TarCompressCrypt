using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
            serviceCollection.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            serviceCollection.AddSingleton<ExternalDependencies>();
            serviceCollection.AddSingleton<TarCompressCrypt>();
            serviceCollection.AddSingleton<EncryptionCommands>();
            serviceCollection.AddSingleton<CompressionCommands>();
            serviceCollection.AddSingleton<BenchmarkRunner>();
            serviceCollection.AddSingleton<BenchmarkOptionHelper>();
            serviceCollection.AddSingleton<BenchmarkIterationGenerator>();
            serviceCollection.AddSingleton(_ => new CancellationTokenSource());
        }
    }
}
