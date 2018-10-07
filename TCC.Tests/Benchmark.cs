using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TCC.Lib.Benchmark;
using TCC.Lib.Dependencies;
using TCC.Lib.Helpers;
using TCC.Lib.Options;
using Xunit;

namespace TCC.Tests
{
    public class Benchmark
    {
        [Fact]
        public async Task SimpleBench()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddTcc();

            IServiceProvider provider = serviceCollection.BuildServiceProvider();

            await provider.GetRequiredService<ExternalDependencies>().EnsureAllDependenciesPresent();

            var op = await provider
                .GetRequiredService<BenchmarkRunner>()
                .RunBenchmark(new BenchmarkOption()
                {
                    Content = BenchmarkContent.Both,
                    Algorithm = BenchmarkCompressionAlgo.All,
                    FileSize = 2048,
                    NumberOfFiles = 2,
                    Ratios = "1",
                    Cleanup = true
                });
            op.ThrowOnError();
        }

        [Fact]
        public void GenerateOptions()
        {
            var options = new BenchmarkOption()
            {
                Content = BenchmarkContent.Both,
                Algorithm = BenchmarkCompressionAlgo.All,
                FileSize = 2048,
                NumberOfFiles = 2,
                Ratios = "1",
                Cleanup = true
            };
            var content = new List<BenchmarkTestContent>()
            {
                new BenchmarkTestContent("", true, BenchmarkContent.Ascii),
                new BenchmarkTestContent("", true, BenchmarkContent.Binary),
            };
            var helper = new BenchmarkIterationGenerator();
            var iterations = helper.GenerateBenchmarkIteration(options, content).ToList();
            Assert.Equal(12, iterations.Count);
        }
    }
}
