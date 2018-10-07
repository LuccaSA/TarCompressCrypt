using System;
using TCC.Lib.Options;

namespace TCC.Lib.Benchmark
{
    public class BenchmarkTestContent
    {
        public BenchmarkTestContent(string source, bool shouldDelete, BenchmarkContent content)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ShouldDelete = shouldDelete;
            Content = content;
        }

        public string Source { get; }
        public bool ShouldDelete { get; }
        public BenchmarkContent Content { get; }
    }
}