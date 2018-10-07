namespace TCC.Lib.Helpers
{
    public enum Fail
    {
        /// <summary>
        /// Don't fail loop on exception, return a fault task
        /// </summary>
        Default,
        /// <summary>
        /// Fail loop as soon as an exception happens, return a successful task, with exceptions in ParallelizedSummary
        /// </summary>
        Fast,
        /// <summary>
        /// Don't fail loop on exception, return a successful task, with exceptions in ParallelizedSummary
        /// </summary>
        Smart
    }
}