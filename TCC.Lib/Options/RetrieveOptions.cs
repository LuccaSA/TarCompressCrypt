using System;
using System.IO;
using TCC.Lib.Database;

namespace TCC.Lib.Options;

public class RetrieveOptions : DecompressOption, INetworkStorageOptions
{
    public UploadMode? DownloadMode { get; set; }
    public bool All { get; set; } = false;
    public BackupMode Mode { get; set; }
    public string SourceMachine { get; set; }
    public string SourceArchive { get; set; }
    public DateTime BeforeDateTime { get; set; } = DateTime.UtcNow.AddDays(1);
    public DirectoryInfo DownloadDestinationDir => new (Path.Combine(DestinationDir, "ENCRYPTED"));
    public DirectoryInfo DecryptionDestinationDir => new (Path.Combine(DestinationDir, "DECRYPTED"));
    public bool FolderPerDay { get; set; }
    public string AzBlobUrl { get; set; }
    public string AzBlobContainer { get; set; }
    public string AzBlobSaS { get; set; }
    public int? AzThread { get; set; }
    public string GoogleStorageBucketName { get; set; }
    public string GoogleStorageCredential { get; set; }
    public string S3AccessKeyId { get; set; }
    public string S3SecretAcessKey { get; set; }
    public string S3Host { get; set; }
    public string S3BucketName { get; set; }
    public string S3Region { get; set; }
    public string SearchPrefix {
        get
        {
            return string.IsNullOrWhiteSpace(SourceArchive) ? SourceMachine : $"{SourceMachine}/{SourceArchive}";
        }
    }
}