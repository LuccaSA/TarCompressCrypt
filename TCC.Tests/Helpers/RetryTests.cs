using System;
using System.Collections.Generic;
using System.Text;
using TCC.Lib.Helpers;
using Xunit;

namespace TCC.Tests.Helpers
{
    public class RetryTests
    {
        [Fact]
        public void ValidRetry()
        {
            int retry = 0;

            var ok1 = Retry.CanRetryIn(out TimeSpan retry1, ref retry);
            var ok2 = Retry.CanRetryIn(out TimeSpan retry2, ref retry);
            var ok3 = Retry.CanRetryIn(out TimeSpan retry3, ref retry);
            var ok4 = Retry.CanRetryIn(out TimeSpan retry4, ref retry);

            Assert.True(ok1);
            Assert.True(ok2);
            Assert.True(ok3);
            Assert.False(ok4);

            Assert.True(retry1 >= TimeSpan.FromSeconds(1) && retry1 <= TimeSpan.FromSeconds(2));
            Assert.True(retry2 >= TimeSpan.FromSeconds(3) && retry2 <= TimeSpan.FromSeconds(4));
            Assert.True(retry3 >= TimeSpan.FromSeconds(14) && retry3 <= TimeSpan.FromSeconds(15));
            Assert.Equal(TimeSpan.Zero,retry4);
            ;
        }
    }
}
