using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using TCC.Lib.Helpers;

namespace TCC.Tests.Helpers
{
    public class EnumerableExtensionsTests
    {
        [Theory]
        [InlineData(new[] { 1, 2, 3, 4, 5 }, new[] { 4, 2, 1, 3 }, new[] { 5, 4, 2, 1, 3 })]
        [InlineData(new[] { 1, 2, 3, 4, 4, 5 }, new[] { 4, 2, 1, 3 }, new[] { 5, 4, 4, 2, 1, 3 })]
        [InlineData(new[] { 1, 2, 3, 4, 5, 5 }, new[] { 4, 2, 1, 3 }, new[] { 5, 5, 4, 2, 1, 3 })]
        [InlineData(new[] { 1, 2, 3 }, new[] { 2, 1, 3, 5, 4 }, new[] { 2, 1, 3 })]
        [InlineData(new[] { 1 }, new[] { 1 }, new[] { 1 })]
        public async Task YieldAll(int[] src, int[] ord, int[] exp)
        {
            var source = src.ToItems();
            var order = ord.ToItems();
            var expected = new Queue<int>(exp);

            var ordered = source.OrderBySequence(order,
                i => i.Value,
                o => o.Value,
                 (i, o) => Task.CompletedTask);

            await foreach (var v in ordered)
            {
                var e = expected.Dequeue();
                Assert.Equal(e, v.Value);
            }

            Assert.Empty(expected);
        }
    }

    public static class ItemHelper
    {
        public static List<Item<T>> ToItems<T>(this IEnumerable<T> source)
        {
            return source.Select(i => new Item<T> { Value = i }).ToList();
        }
    }

    public class Item<T>
    {
        public T Value { get; set; }
    }
}
