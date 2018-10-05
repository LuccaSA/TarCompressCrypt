using System.Threading.Tasks;
using Xunit;

namespace TCC.Tests
{
    public class CommandLineTest
    {
        [Fact]
        public async Task BenchmarkCall()
        {
            await Program.Main("benchmark -e false -a Zstd -r 1 -c Ascii -n 1 -s 4096 -t 1".Split(" "));
        }

        [Fact]
        public async Task CompressCall()
        {
            await Program.Main("compress .\\TCC.dll -a Zstd -r 1 -f -p 1234 -t 1 -o .\\test".Split(" "));
        }
    }
}
