using System;

namespace TCC.Lib.Database
{
    public class BlockJob
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public Job Job { get; set; }
        public BackupMode BackupMode { get; set; }
        public String FullSourcePath { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public long Size { get; set; }
        public bool Success { get; set; }
        public string Exception { get; set; }
    }
}