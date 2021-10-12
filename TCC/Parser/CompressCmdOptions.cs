using System.Collections.Generic;
using CommandLine;
using TCC.Lib;
using TCC.Lib.Database;
using TCC.Lib.Options;

namespace TCC.Parser
{
    [Verb("compress", HelpText = "Compress specified files/folders")]
    public class CompressCmdOptions : BaseCmdOptions
    {
        [Value(0, Required = true, MetaName = "Source", HelpText = "Files or Folders to compress")]
        public IEnumerable<string> Source { get; set; }

        [Option('i', "individual", HelpText = "Individual mode : create distinct archives for each file / folder in source")]
        public bool Individual { get; set; }

        [Option('a', "algorithm", HelpText = "Algorithm : Lz4 (default), Brotli, Zstd", Default = CompressionAlgo.Lz4)]
        public CompressionAlgo Algorithm { get; set; }

        [Option('r', "ratio", HelpText = "Compression ratio. Valid values are : lz4 [1,9], brotli [1,11], zstd [1,19] ")]
        public int Ratio { get; set; }

        [Option('m', "mode", HelpText = "Backup mode : Full or Diff. (Default = Full if no Diff, and Diff if Full already exists)\n" +
                                        "Full : force a full backup for all sources.\n" +
                                        "Diff : Archive delta since last full")]
        public BackupMode? BackupMode { get; set; }

        [Option("retries", HelpText = "Number seconds retring a failed compression job")]
        public int? RetryPeriodInSeconds { get; set; }

        [Option("filter", HelpText = "Optional filters")]
        public IEnumerable<string> Filter { get; set; }

        [Option("exclude", HelpText = "Exclusion filters")]
        public IEnumerable<string> Exclude { get; set; }

        [Option("folderPerDay", HelpText = "Creates a sub folder per day")]
        public bool FolderPerDay { get; set; }

        [Option("maximumRetention", HelpText = "Maximum retention in minutes")]
        public int MaximumRetentionMinutes { get; set; }

        [Option("boostRatio", HelpText = "When more than 90% of Diff will be processed, allow to multiply the number of thread.\n"
                                         + "Ex : boostRatio 4 when having 8 thread will switch process on 32 threads")]
        public int? BoostRatio { get; set; }

        [Option("cleanupTime", HelpText = "Specify the time, in hours, after which the backup files are deleted. If no time is specified, then no backup files are deleted.")]
        public int? CleanupTime { get; set; }


        [Option("azBlobUrl", HelpText = "Azure blob storage URL")]
        public string AzBlobUrl { get; set; }

        [Option("azBlobContainer", HelpText = "Azure blob storage container id")]
        public string AzBlobContainer { get; set; }

        [Option("azBlobSaS", HelpText = "Azure blob storage SaS token")]
        public string AzSaS { get; set; }

        [Option("googleStorageBucketName", HelpText = "Google Cloud Storage destination bucket")]
        public string GoogleStorageBucketName { get; set; }

        [Option("googleStorageCredential", HelpText = "Google Cloud Storage credential json, either full path or base64")]
        public string GoogleStorageCredentialFile { get; set; }

        [Option("azThread", HelpText = "Azure blob maximum parallel threads")]
        public int? AzThread { get; set; }

        [Option("uploadMode", HelpText = "Upload mode")]
        public UploadMode? UploadMode { get; set; }
    }
}