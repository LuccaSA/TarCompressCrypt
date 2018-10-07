using System;
using System.Collections.Generic;
using System.Linq;

namespace TCC.Lib.Helpers
{
    public class ParallelizedSummary
    {
        public IEnumerable<Exception> Exceptions { get; }

        public bool IsCancelled { get; }

        public bool IsSucess => !IsCancelled && (Exceptions == null || !Exceptions.Any());

        public ParallelizedSummary(IEnumerable<Exception> exceptions, bool isCancelled)
        {
            Exceptions = exceptions;
            IsCancelled = isCancelled;
        }
    }
}