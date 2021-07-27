using Azure.Storage.Blobs;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Dependencies;
using TCC.Lib.Options;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace TCC.Lib.ObjectStorage
{
    public interface IObjectStorageRemoteServer
    {
        Task<UploadResponse> UploadAsync(string targetPath, Stream data, CancellationToken token);

        public async Task<UploadResponse> UploadAsync(FileInfo file, DirectoryInfo rootFolder, CancellationToken token)
        {
            string targetPath = file.GetRelativeTargetPathTo(rootFolder);
            await using FileStream uploadFileStream = File.OpenRead(file.FullName);
            return await UploadAsync(targetPath, uploadFileStream, token);
        }
    }

    public static class RemoteServerFactory
    {
        public static async Task<IObjectStorageRemoteServer> CreateRemoteServerAsync(this CompressOption option, CancellationToken token)
        {
            switch (option.UploadMode)
            {
                case UploadMode.AzureSdk:
                    var client = new BlobServiceClient(new Uri(option.AzBlobUrl + "/" + option.AzBlobContainer + "?" + option.AzSaS));
                    BlobContainerClient container = client.GetBlobContainerClient(option.AzBlobContainer);
                    return new AzureRemoteServer(container);
                case UploadMode.GoogleCloudStorage:
                    StorageClient storage = await GetGoogleStorageClient(option, token);
                    return new GoogleRemoteServer(storage, option.GoogleStorageBucketName);
                case UploadMode.None:
                case null:
                    return new NoneRemoteServer();
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

    public class NoneRemoteServer : IObjectStorageRemoteServer
    {
        public Task<UploadResponse> UploadAsync(string targetPath, Stream data, CancellationToken token)
        {
            return Task.FromResult(new UploadResponse { IsSuccess = true, RemoteFilePath = targetPath });
        }
    }

    public class AzureRemoteServer : IObjectStorageRemoteServer
    {
        private readonly BlobContainerClient _container;

        public AzureRemoteServer(BlobContainerClient container)
        {
            _container = container;
        }

        public async Task<UploadResponse> UploadAsync(string targetPath, Stream data, CancellationToken token)
        {
            var result = await _container.UploadBlobAsync(targetPath, data, token);
            var response = result.GetRawResponse();
            return new UploadResponse
            {
                IsSuccess = response.Status == 201,
                ErrorMessage = response.ReasonPhrase,
                RemoteFilePath = targetPath
            };
        }
    }

    public class GoogleRemoteServer : IObjectStorageRemoteServer
    {
        internal StorageClient Storage { get; }
        internal string BucketName { get; }

        public GoogleRemoteServer(StorageClient storage, string bucketName)
        {
            Storage = storage;
            BucketName = bucketName;
        }

        public async Task<UploadResponse> UploadAsync(string targetPath, Stream data, CancellationToken token)
        {
            try
            {
                Object uploaded = await Storage.UploadObjectAsync(BucketName, targetPath, null, data, cancellationToken: token);
            }
            catch (Exception e)
            {
                return new UploadResponse
                {
                    IsSuccess = false,
                    RemoteFilePath = targetPath,
                    ErrorMessage = e.Message
                };
            }
            return new UploadResponse
            {
                IsSuccess = true, 
                RemoteFilePath = targetPath
            };
        }
    }

    public class UploadResponse
    {
        public string RemoteFilePath { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }
}
