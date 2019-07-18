using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TCC.Lib.Helpers
{
    public static class EnumerableExtensions
    {
        public static async IAsyncEnumerable<T> OrderBySequence<T, TOrder, TProperty>(this IEnumerable<T> source, IEnumerable<TOrder> order,
            Func<T, TProperty> sourcePropertySelector,
            Func<TOrder, TProperty> orderPropertySelector,
            Func<T, TOrder, Task> itemMatch)
            where T : class
            where TOrder : class
        { 
            var fromOrder = new List<T>();

            var src = source
                .GroupBy(sourcePropertySelector)
                .ToDictionary(i=> i.Key, v => v.ToList());

            foreach (var ord in order)
            {
               var orderedProperty = orderPropertySelector(ord);
               if (src.ContainsKey(orderedProperty))
               {
                   foreach (var i in src[orderedProperty])
                   {
                       await itemMatch(i, ord);
                       fromOrder.Add(i);
                   }
                   src.Remove(orderedProperty);
               }
            }

            foreach (var v in src.Values.SelectMany(i => i))
            {
                yield return v;
            }

            foreach (var v in fromOrder)
            {
                yield return v;
            }
        }

        public static IEnumerable<T> Yield<T>(this T source)
        {
            yield return source;
        }

        public static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IEnumerable<T> enumerable)
        {
            await Task.Yield();
            foreach (var item in enumerable)
            {
                yield return item;
            }
        }
    }
}