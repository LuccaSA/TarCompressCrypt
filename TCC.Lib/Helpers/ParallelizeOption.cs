using System;

namespace TCC.Lib.Helpers
{
    public class ParallelizeOption
    {
        public int MaxDegreeOfParallelism { get; set; }
        public Fail FailMode { get; set; }
    }

    public class ParallelMonitor<T>
    {
        public ParallelMonitor(int optionMaxDegreeOfParallelism)
        {
            if (optionMaxDegreeOfParallelism <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(optionMaxDegreeOfParallelism));
            }
            ActiveItem = new T[optionMaxDegreeOfParallelism];
        }

        public T[] ActiveItem { get; }
    }
}