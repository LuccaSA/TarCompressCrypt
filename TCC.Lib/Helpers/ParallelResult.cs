using System;

namespace TCC.Lib.Helpers
{
    public class ParallelResult<T>
    {
        public ParallelResult(T item, ExecutionState executionState)
        {
            Item = item;
            ExecutionState = executionState;
        }
        public ParallelResult(T item, ExecutionState executionState, Exception exception)
        {
            Item = item;
            ExecutionState = executionState;
            Exception = exception;
        }
        public T Item { get; }
        public ExecutionState ExecutionState { get; }
        public Exception Exception { get; }
    }
}