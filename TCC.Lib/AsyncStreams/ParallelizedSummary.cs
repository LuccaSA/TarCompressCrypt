using System;
using System.Collections.Generic;
using System.Linq;

namespace TCC.Lib.AsyncStreams
{
    public class ParallelizedSummary
    {
        public List<Exception> Exceptions { get; }

        public bool IsCanceled { get; }

        public bool IsSuccess => !IsCanceled && Exceptions.Count == 0;

        internal ParallelizedSummary(IEnumerable<Exception> exceptions, bool isCanceled)
        {
            Exceptions = new List<Exception>(exceptions ?? Enumerable.Empty<Exception>());
            IsCanceled = isCanceled;
        }
    }
}