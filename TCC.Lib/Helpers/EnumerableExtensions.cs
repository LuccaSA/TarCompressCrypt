using System;
using System.Collections.Generic;
using System.Linq;

namespace TCC.Lib.Helpers
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> OrderBySequence<T, TOrder, TProperty>(this IEnumerable<T> source, IEnumerable<TOrder> order,
            Func<T, TProperty> sourcePropertySelector,
            Func<TOrder, TProperty> orderPropertySelector,
            Action<T, TOrder> itemMatch)
            where T : class
            where TOrder : class
        {
            var dic = new Dictionary<TProperty, List<T>>();
            var queue = new Queue<TOrder>(order);

            var current = queue.Dequeue();
            var currentProperty = orderPropertySelector(current);

            bool queueEmpty = false;

            foreach (var item in source)
            {
                var sourceProperty = sourcePropertySelector(item);

                if (!queueEmpty && Equals(currentProperty, sourceProperty))
                {
                    itemMatch(item, current);
                    yield return item;

                    if (queue.Count != 0)
                    {
                        current = queue.Dequeue();
                        currentProperty = orderPropertySelector(current);
                    }
                    else
                    {
                        queueEmpty = true;
                    }
                }
                else
                {
                    if (dic.ContainsKey(sourceProperty))
                    {
                        dic[sourceProperty].Add(item);
                    }
                    else
                    {
                        dic[sourceProperty] = new List<T> { item };
                    }
                }
            }

            while (true)
            {
                if (!queueEmpty && dic.ContainsKey(currentProperty))
                {
                    foreach (var item in dic[currentProperty])
                    {
                        itemMatch(item, current);
                        yield return item;
                    }
                    dic.Remove(currentProperty);
                }

                if (queue.Count == 0)
                {
                    break;
                }

                current = queue.Dequeue();
                currentProperty = orderPropertySelector(current);
            }

            foreach (var item in dic.Values.SelectMany(i => i))
            {
                itemMatch(item, null);
                yield return item;
            }
        }

        public static IEnumerable<T> Foreach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            foreach (T item in source)
            {
                action(item);
                yield return item;
            }
        }
    }
}