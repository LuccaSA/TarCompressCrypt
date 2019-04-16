﻿using System;
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
        [Fact(Skip ="block")]
        public async Task SimpleBench()
        {
            var services = new ServiceCollection();
            services.AddTcc();
            services.PostConfigure<TccSettings>(i =>
            {
                i.ConnectionString = "Data Source=:memory:";
            });

            IServiceProvider provider = services.BuildServiceProvider();
            using (var scope = provider.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<ExternalDependencies>().EnsureAllDependenciesPresent();

                var op = await scope.ServiceProvider
                    .GetRequiredService<BenchmarkRunner>()
                    .RunBenchmark(new BenchmarkOption
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
        }

        [Fact]
        public void GenerateOptions()
        {
            var options = new BenchmarkOption
            {
                Content = BenchmarkContent.Both,
                Algorithm = BenchmarkCompressionAlgo.All,
                FileSize = 2048,
                NumberOfFiles = 2,
                Ratios = "1",
                Cleanup = true
            };
            var content = new List<BenchmarkTestContent>
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
