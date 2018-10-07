using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Helpers;
using Xunit;

namespace TCC.Tests.Helpers
{
    public class LockTest
    {
        [Fact]
        public async Task LockTestAsync()
        {
            var lockFile = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".lock"));
            int counter = 0;
            var tasks = Enumerable.Range(0, 8).Select(i => Locker()).ToArray();
            await Task.WhenAll(tasks);
            Assert.Equal(8,counter);
            lockFile.Delete();

            async Task Locker()
            {
                await lockFile.Lock(async () =>
                {
                    await Task.Delay(100);
                    Interlocked.Increment(ref counter);
                });
            }
        }
    }
}
