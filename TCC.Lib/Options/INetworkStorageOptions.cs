namespace TCC.Lib.Options;

public interface INetworkStorageOptions
{
    string AzBlobUrl { get; set; }
    string AzBlobContainer { get; set; }
    string AzBlobSaS { get; set; }
    int? AzThread { get; set; }
    string GoogleStorageBucketName { get; set; }
    string GoogleStorageCredential { get; set; }
    string S3AccessKeyId { get; set; }
    string S3SecretAcessKey { get; set; }
    string S3Host { get; set; }
    string S3BucketName { get; set; }
    string S3Region { get; set; }
}