using System;
using TCC.Lib.Helpers;
using Xunit;

namespace TCC.Tests.Helpers
{
    public class StringTest
    {
        [Fact]
        public void TimeSpanString()
        {
            var ts = new TimeSpan(1, 2, 3, 4, 42);
        
            Assert.Equal("1d 2h 3m 4s 42ms", ts.HumanizedTimeSpan(5));
        }

        [Fact]
        public void PadTest()
        {
            string n = null;
            Assert.Equal(3,n.Pad(3).Length);
            Assert.Equal(3, String.Empty.Pad(3).Length);
            Assert.Equal(3, "          ".Pad(3).Length);
        }
    }
}