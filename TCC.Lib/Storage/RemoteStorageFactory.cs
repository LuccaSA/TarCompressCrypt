using Amazon.Runtime;
using Amazon.S3;
using Azure.Storage.Blobs;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Storage
{
    public static class RemoteStorageFactory
    {
        public static async Task<IRemoteStorage> GetRemoteStorageAsync(this CompressOption option, ILogger logger, CancellationToken token)
        {
            switch (option.UploadMode)
            {
                case UploadMode.AzureSdk:
                {
                    if (string.IsNullOrEmpty(option.AzBlobUrl)
                        || string.IsNullOrEmpty(option.AzBlobContainer)
                        || string.IsNullOrEmpty(option.AzBlobSaS))
                    {
                        logger.LogCritical("Configuration error for azure blob upload");
                        return new NoneRemoteStorage();
                    }
                    var client = new BlobServiceClient(new Uri(option.AzBlobUrl + "/" + option.AzBlobContainer + "?" + option.AzBlobSaS));
                    BlobContainerClient container = client.GetBlobContainerClient(option.AzBlobContainer);
                    return new AzureRemoteStorage(container);
                }
                case UploadMode.GoogleCloudStorage:
                {
                    if (string.IsNullOrEmpty(option.GoogleStorageCredential)
                        || string.IsNullOrEmpty(option.GoogleStorageBucketName))
                    {
                        logger.LogCritical("Configuration error for google storage upload");
                        return new NoneRemoteStorage();
                    }
                    StorageClient storage = await GoogleAuthHelper.GetGoogleStorageClientAsync(option.GoogleStorageCredential, token);
                    return new GoogleRemoteStorage(storage, option.GoogleStorageBucketName);
                }
                case UploadMode.S3:
                    if (string.IsNullOrEmpty(option.S3AccessKeyId)
                        || string.IsNullOrEmpty(option.S3Host)
                        || string.IsNullOrEmpty(option.S3Region)
                        || string.IsNullOrEmpty(option.S3BucketName)
                        || string.IsNullOrEmpty(option.S3SecretAcessKey))
                    {
                        logger.LogCritical("Configuration error for S3 upload");
                        return new NoneRemoteStorage();
                    }

                    var credentials = new BasicAWSCredentials(option.S3AccessKeyId, option.S3SecretAcessKey);
                    var s3Config = new AmazonS3Config()
                    {
                        AuthenticationRegion = option.S3Region,
                        ServiceURL = option.S3Host,
                    };

                    return new S3RemoteStorage(new AmazonS3Client(credentials, s3Config), option.S3BucketName);
                case UploadMode.None:
                case null:
                    return new NoneRemoteStorage();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}