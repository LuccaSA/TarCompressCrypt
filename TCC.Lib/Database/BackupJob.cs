using System;
using System.Collections.Generic;

namespace TCC.Lib.Database
{
    public class BackupJob
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public List<BackupBlockJob> BlockJobs { get; set; }
    }

    public class RestoreJob
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public List<RestoreBlockJob> BlockJobs { get; set; }
    }
}