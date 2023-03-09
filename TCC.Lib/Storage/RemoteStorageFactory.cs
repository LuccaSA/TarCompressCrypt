using Amazon.Runtime;
using Amazon.S3;
using Azure.Storage.Blobs;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Storage
{
    public static class RemoteStorageFactory
    {
        public static async IAsyncEnumerable<IRemoteStorage> GetRemoteStoragesAsync(this CompressOption option, ILogger logger, [EnumeratorCancellation] CancellationToken token)
        {
            if (option.UploadMode.HasValue)
            {
                option.UploadModes = option.UploadModes.Append(option.UploadMode.Value);
            }
            option.UploadModes = option.UploadModes.Distinct();

            foreach(var mode in option.UploadModes)
            {
                yield return await mode.BuildSingleRemoteStorageAsync(option, logger, token);
            }
        }
        
        public static async Task<IRemoteStorage> GetRemoteStorageAsync(this RetrieveOptions option, ILogger logger, CancellationToken token)
        {
            if (option.DownloadMode.HasValue)
            {
                return await option.DownloadMode.Value.BuildSingleRemoteStorageAsync(option, logger, token);
            }
            return new NoneRemoteStorage();
        }

        private static async Task<IRemoteStorage> BuildSingleRemoteStorageAsync(this UploadMode mode, INetworkStorageOptions option, ILogger logger, CancellationToken token)
        {
            try
            {
                switch (mode)
                {
                    case UploadMode.AzureSdk:
                        return option.BuildAzureStorage();
                    case UploadMode.GoogleCloudStorage:
                        return await option.BuildGoogleStorageAsync(token);
                    case UploadMode.S3:
                        return option.BuildS3Storage();
                    case UploadMode.None:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (ApplicationException ex)
            {
                logger.LogCritical(ex.Message);
            }
            return new NoneRemoteStorage();
        }

        private static async Task<GoogleRemoteStorage> BuildGoogleStorageAsync(this INetworkStorageOptions option, CancellationToken token)
        {
            if (string.IsNullOrEmpty(option.GoogleStorageCredential))
            {
                throw new ApplicationException("[GoogleStorage] Configuration error: missing/invalid --GoogleStorageCredential");
            }
            if (string.IsNullOrEmpty(option.GoogleStorageBucketName))
            {
                throw new ApplicationException(
                    "[GoogleStorage] Configuration error: missing/invalid --GoogleStorageBucketName");
            }
            StorageClient storage = await GoogleAuthHelper.GetGoogleStorageClientAsync(option.GoogleStorageCredential, token);
            return new GoogleRemoteStorage(storage, option.GoogleStorageBucketName);
        }

        private static S3RemoteStorage BuildS3Storage(this INetworkStorageOptions option)
        {
            if (string.IsNullOrEmpty(option.S3AccessKeyId))
            {
                throw new ApplicationException("[S3Storage] Configuration error: missing/invalid --S3AccessKeyId");
            }
            if (string.IsNullOrEmpty(option.S3Host))
            {
                throw new ApplicationException("[S3Storage] Configuration error: missing/invalid --S3Host");
            }
            if (string.IsNullOrEmpty(option.S3Region))
            {
                throw new ApplicationException("[S3Storage] Configuration error: missing/invalid --S3Region");
            }
            if (string.IsNullOrEmpty(option.S3BucketName))
            {
                throw new ApplicationException("[S3Storage] Configuration error: missing/invalid --S3BucketName");
            }
            if (string.IsNullOrEmpty(option.S3SecretAcessKey))
            {
                throw new ApplicationException("[S3Storage] Configuration error: missing/invalid --S3SecretAcessKey");
            }

            var credentials = new BasicAWSCredentials(option.S3AccessKeyId, option.S3SecretAcessKey);
            var s3Config = new AmazonS3Config
            {
                AuthenticationRegion = option.S3Region,
                ServiceURL = option.S3Host,
            };

            if (option is CompressOption compressOption)
            {
                return new S3RemoteStorage(new AmazonS3Client(credentials, s3Config), option.S3BucketName, compressOption.S3MultipartThreshold.ParseSize(), compressOption.S3MultipartSize.ParseSize());
            }
            return new S3RemoteStorage(new AmazonS3Client(credentials, s3Config), option.S3BucketName);
        }

        private static AzureRemoteStorage BuildAzureStorage(this INetworkStorageOptions option)
        {
            if (string.IsNullOrEmpty(option.AzBlobUrl))
            {
                throw new ApplicationException("[AzureBlobStorage] Configuration error: missing/invalid --AzBlobUrl");
            }

            if (string.IsNullOrEmpty(option.AzBlobContainer))
            {
                throw new ApplicationException("[AzureBlobStorage] Configuration error: missing/invalid --AzBlobContainer");
            }

            if (string.IsNullOrEmpty(option.AzBlobSaS))
            {
                throw new ApplicationException("[AzureBlobStorage] Configuration error: missing/invalid --AzBlobSaS");
            }

            var client =
                new BlobServiceClient(new Uri(option.AzBlobUrl + "/" + option.AzBlobContainer + "?" + option.AzBlobSaS));
            BlobContainerClient container = client.GetBlobContainerClient(option.AzBlobContainer);
            return new AzureRemoteStorage(container);
        }
    }
}