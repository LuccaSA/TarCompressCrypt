using System;
using System.Collections.Generic;

namespace TCC.Lib.Database
{
    public class Job
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public List<BlockJob> BlockJobs { get; set; }
    }
}