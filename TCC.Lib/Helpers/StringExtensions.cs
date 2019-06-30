using System;
using System.IO;

namespace TCC.Lib.Helpers
{
    public static class StringExtensions
    {
        public static string Escape(this string str)
        {
            return '"' + str.Trim('"') + '"';
        }

        public static string HumanizedBandwidth(this double bandwidth, int decimals = 2)
        {
            var ordinals = new[] { "", "K", "M", "G", "T", "P", "E" };
            var rate = (decimal)bandwidth;
            var ordinal = 0;
            while (rate > 1024)
            {
                rate /= 1024;
                ordinal++;
            }
            return String.Format("{0:n" + decimals + "} {1}b/s", Math.Round(rate, decimals, MidpointRounding.AwayFromZero), ordinals[ordinal]);
        }

        public static string HumanizedTimeSpan(this TimeSpan t, int parts = 2)
        {
            string result = string.Empty;
            if (t.TotalDays >= 1 && parts > 0)
            {
                result += $"{t.Days}d ";
                parts--;
            }
            if (t.TotalHours >= 1 && parts > 0)
            {
                result += $"{t.Hours}h ";
                parts--;
            }
            if (t.TotalMinutes >= 1 && parts > 0)
            {
                result += $"{t.Minutes}m ";
                parts--;
            }
            if (t.Seconds >= 1 && parts > 0)
            {
                result += $"{t.Seconds}s ";
                parts--;
            }
            if (t.Milliseconds >= 1 && parts > 0)
            {
                result += $"{t.Milliseconds}ms";
            }
            return result.TrimEnd();
        }

        public static string Pad(this string source, int length)
        {
            if (source == null)
            {
                return new string(' ', length);
            }
            return source.Length > length ? source.Substring(0, length) : source.PadLeft(length, ' ');
        }

        public static (string Name, DateTime? Date) ExtractArchiveNameAndDate(this string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            string segment = Path.GetFileNameWithoutExtension(filePath);
            if (segment.EndsWith(".diff") || segment.EndsWith(".full"))
            {
                segment = segment.Substring(0, segment.Length - 5);
            }

            var lastSegment = segment.LastIndexOf('_');
            if (lastSegment > 0)
            {
                string name = segment.Substring(0, lastSegment);
                string date = segment.Substring(lastSegment + 1);
                if (date.TryParseArchiveDateTime(out var dt))
                {
                    return (name, dt);
                }
                return (name, null);
            }
            return (segment, null);
        }
    }
}