using System;

namespace TCC.Lib.Database
{
    public class BackupBlockJob
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public BackupJob Job { get; set; }
        public BackupMode BackupMode { get; set; }
        public String FullSourcePath { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public long Size { get; set; }
        public bool Success { get; set; }
        public string Exception { get; set; }
    }

    public class RestoreBlockJob
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public RestoreJob Job { get; set; }
        public BackupMode BackupMode { get; set; }
        public String FullSourcePath { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public long Size { get; set; }
        public bool Success { get; set; }
        public string Exception { get; set; }
    }
}