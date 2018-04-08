using System;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Command;
using Xunit;

namespace TCC.Tests.Command
{
    public class CommandTest
    {
        [Fact]
        public async Task Timeout()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await "TIMEOUT /T 42 /NOBREAK >NUL".Run(null, CancellationToken.None, TimeSpan.FromMilliseconds(100));
            });
        }

        [Fact]
        public async Task Kill()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            var t1 = "TIMEOUT /T 5 /NOBREAK >NUL".Run(null, cts.Token, TimeSpan.FromSeconds(5));

#pragma warning disable 4014
            Task.Run(async () =>
#pragma warning restore 4014
            {
                await Task.Delay(100);
                cts.Cancel();
            });

            await Assert.ThrowsAsync<TaskCanceledException>(async () => { await t1; });
        }

        [Fact]
        public async Task StdOut()
        {
            var cmd = "echo hello world";
            var result = await cmd.Run(null, CancellationToken.None);
            Assert.Equal("hello world", result.Output.Trim());
            Assert.True(result.IsSuccess);
            Assert.False(result.HasError);
            Assert.Equal(0,result.ExitCode);
            Assert.Equal(cmd,result.Command);
        }

        [Fact]
        public async Task StdError()
        {
            var cmd = "echo hello world 1>&2 && EXIT 1";
            var result = await cmd.Run(null, CancellationToken.None);
            Assert.Equal("hello world", result.Errors.Trim());
            Assert.False(result.IsSuccess);
            Assert.True(result.HasError);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal(cmd, result.Command);
        }
    }
}
