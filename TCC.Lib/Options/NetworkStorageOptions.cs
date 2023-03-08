namespace TCC.Lib.Options;

public class NetworkStorageOptions : TccOption
{
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
}