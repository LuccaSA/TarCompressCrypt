using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.AsyncStreams;
using Xunit;

namespace TCC.Tests.Tasks
{
    public class AsyncStreamTest
    {
        [Fact]
        public async Task EnumerableExceptionAsync()
        {
            var list = Enumerable.Range(0, 100).Select(i =>
            {
                if (i == 42)
                {
                    throw new Exception();
                }
                return i;
            });

            var asyncStream = list.AsAsyncStream(CancellationToken.None);

            var result = await asyncStream.AsReadOnlyCollectionAsync();

            Assert.NotEmpty(result);
        }
    }
}
