using System;
using System.Collections.Generic;
using System.Text;
using TCC.Lib.Helpers;
using Xunit;

namespace TCC.Tests
{
   public class BlockTests
    {
        [Fact]
        public void GetName()
        {
            var name = "DATA_NAME_20190630142158.full.tarzstdaes";
            var archName = name.ExtractArchiveNameAndDate();

            Assert.Equal("DATA_NAME", archName.Name);
            Assert.Equal(new DateTime(2019,06,30,14,21,58, DateTimeKind.Utc), archName.Date);
        }
    }
}
