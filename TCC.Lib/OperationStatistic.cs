namespace TCC.Lib
{
    public class OperationStatistic
    {
        /// <summary>
        /// Mean throughput in Mbps per thread
        /// </summary>
        public double Mean { get; set; }
        public double Variance { get; set; }
        public double StandardDeviation { get; set; }
        public double StandardError { get; set; }
        /// <summary>
        /// Average throughput on all threads
        /// </summary>
        public double AverageThroughput { get; set; }
    }
}