using Amazon.Runtime;
using Amazon.S3;
using Azure.Storage.Blobs;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Storage
{
    public static class RemoteStorageFactory
    {
        public static async Task<IEnumerable<IRemoteStorage>> GetRemoteStoragesAsync(this CompressOption option, ILogger logger, CancellationToken token)
        {
            var remoteStorages = new List<IRemoteStorage>();

            option.UploadModes = option.UploadModes.Append(option.UploadMode ?? UploadMode.None).Distinct();

            foreach(var mode in option.UploadModes)
            {
                switch (mode)
                {
                    case UploadMode.AzureSdk:
                        {
                            if (string.IsNullOrEmpty(option.AzBlobUrl))
                            {
                                logger.LogCritical("[AzureBlobStorage] Configuration error: missing/invalid --AzBlobUrl");
                                continue;
                            }
                            if (string.IsNullOrEmpty(option.AzBlobContainer))
                            {
                                logger.LogCritical("[AzureBlobStorage] Configuration error: missing/invalid --AzBlobContainer");
                                continue;
                            }
                            if (string.IsNullOrEmpty(option.AzBlobSaS))
                            {
                                logger.LogCritical("[AzureBlobStorage] Configuration error: missing/invalid --AzBlobSaS");
                                continue;
                            }
                            var client = new BlobServiceClient(new Uri(option.AzBlobUrl + "/" + option.AzBlobContainer + "?" + option.AzBlobSaS));
                            BlobContainerClient container = client.GetBlobContainerClient(option.AzBlobContainer);
                            remoteStorages.Add(new AzureRemoteStorage(container));
                            break;
                        }
                    case UploadMode.GoogleCloudStorage:
                        {
                            if (string.IsNullOrEmpty(option.GoogleStorageCredential))
                            {
                                logger.LogCritical("[GoogleStorage] Configuration error: missing/invalid --GoogleStorageCredential");
                                continue;
                            }
                            if (string.IsNullOrEmpty(option.GoogleStorageBucketName))
                            {
                                logger.LogCritical("[GoogleStorage] Configuration error: missing/invalid --GoogleStorageBucketName");
                                continue;
                            }
                            StorageClient storage = await GoogleAuthHelper.GetGoogleStorageClientAsync(option.GoogleStorageCredential, token);
                            remoteStorages.Add(new GoogleRemoteStorage(storage, option.GoogleStorageBucketName));
                            break;
                        }
                    case UploadMode.S3:
                        if (string.IsNullOrEmpty(option.S3AccessKeyId))
                        {
                            logger.LogCritical("[S3Storage] Configuration error: missing/invalid --S3AccessKeyId");
                            continue;
                        }
                        if (string.IsNullOrEmpty(option.S3Host))
                        {
                            logger.LogCritical("[S3Storage] Configuration error: missing/invalid --S3Host");
                            continue;
                        }
                        if (string.IsNullOrEmpty(option.S3Region))
                        {
                            logger.LogCritical("[S3Storage] Configuration error: missing/invalid --S3Region");
                            continue;
                        }
                        if (string.IsNullOrEmpty(option.S3BucketName))
                        {
                            logger.LogCritical("[S3Storage] Configuration error: missing/invalid --S3BucketName");
                            continue;
                        }
                        if (string.IsNullOrEmpty(option.S3SecretAcessKey))
                        {
                            logger.LogCritical("[S3Storage] Configuration error: missing/invalid --S3SecretAcessKey");
                            continue;
                        }

                        var credentials = new BasicAWSCredentials(option.S3AccessKeyId, option.S3SecretAcessKey);
                        var s3Config = new AmazonS3Config
                        {
                            AuthenticationRegion = option.S3Region,
                            ServiceURL = option.S3Host,
                        };

                        remoteStorages.Add(new S3RemoteStorage(
                            new AmazonS3Client(credentials, s3Config),
                            option.S3BucketName,
                            option.S3MultipartThreshold.ParseSize(),
                            (int) option.S3MultipartSize.ParseSize()));
                        break;
                    case UploadMode.None:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return remoteStorages;
        }
    }
}