using System;
using System.Globalization;
using System.IO;
using System.Linq;

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

        public static String HumanizeSize(this long size)
        {
            string[] suf = { "B", "Ko", "Mo", "Go", "To", "Po", "Eo" };
            if (size == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(size);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(size) * num).ToString(CultureInfo.InvariantCulture) + " " + suf[place];
        }

        public static long ParseSize(this string humanizedSize)
        {
            string[] suf = { "b", "ko", "mo", "go", "to", "po", "eo" };
            var size = humanizedSize.Trim().ToLower();
            var number = string.Join("", size.Where(char.IsDigit));
            var unit = size.Substring(size.Length - 2);
            var pow = Array.IndexOf(suf, unit);

            return pow switch
            {
                -1 => long.Parse(number),
                _ => long.Parse(number) * (long)Math.Pow(1024L, pow)
            };
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