using System;

namespace TCC.Lib.Helpers
{
    public struct StreamedValue<T>
    {
        public StreamedValue(T item, ExecutionStatus status)
        {
            Item = item;
            Status = status;
            Exception = null;
        }
        public StreamedValue(T item, ExecutionStatus status, Exception exception)
        {
            Item = item;
            Status = status;
            Exception = exception;
        }
        public T Item { get; }
        public ExecutionStatus Status { get; }
        public Exception Exception { get; }
    }
    
}