using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Channels;

namespace TCC.Lib.Helpers
{
    internal static class ChannelExtensions
    {
        public static ConcurrentQueue<T> InternalQueue<T>(this Channel<T> channel)
        {
            var type = channel.GetType();

            if (type.Name.Contains("UnboundedChannel"))
            {
                var itemsField = channel.GetType().GetField("_items", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
                var value = itemsField.GetValue(channel) as ConcurrentQueue<T>;
                return value;
            }

            throw new NotImplementedException("BoundedChannel : todo");
        }
    }
}
