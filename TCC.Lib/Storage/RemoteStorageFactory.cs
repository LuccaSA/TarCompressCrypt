using Azure.Storage.Blobs;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
                        || string.IsNullOrEmpty(option.AzSaS))
                    {
                        logger.LogCritical("Configuration error for azure blob upload");
                        return new NoneRemoteStorage();
                    }
                    var client = new BlobServiceClient(new Uri(option.AzBlobUrl + "/" + option.AzBlobContainer + "?" + option.AzSaS));
                    BlobContainerClient container = client.GetBlobContainerClient(option.AzBlobContainer);
                    return new AzureRemoteStorage(container);
                }
                case UploadMode.GoogleCloudStorage:
                {
                    if (string.IsNullOrEmpty(option.GoogleStorageCredentialFile)
                        || string.IsNullOrEmpty(option.GoogleStorageBucketName))
                    {
                        logger.LogCritical("Configuration error for google storage upload");
                        return new NoneRemoteStorage();
                    }
                    StorageClient storage = await GetGoogleStorageClient(option, token);
                    return new GoogleRemoteStorage(storage, option.GoogleStorageBucketName);
                }
                case UploadMode.None:
                case null:
                    return new NoneRemoteStorage();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static async Task<StorageClient> GetGoogleStorageClient(CompressOption option, CancellationToken token)
        {
            GoogleCredential credential;
            if (File.Exists(option.GoogleStorageCredentialFile))
            {
                credential = await GoogleCredential.FromFileAsync(option.GoogleStorageCredentialFile, token);
            }
            else
            {
                var decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(option.GoogleStorageCredentialFile));
                credential = GoogleCredential.FromJson(decodedJson);
            }
            return await StorageClient.CreateAsync(credential);
        }
    }
}