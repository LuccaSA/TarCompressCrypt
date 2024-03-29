﻿using Azure.Storage.Blobs;
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
                case UploadMode.None:
                case null:
                    return new NoneRemoteStorage();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}