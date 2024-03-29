﻿using Google.Cloud.Storage.V1;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace TCC.Lib.Storage
{
    public class GoogleRemoteStorage : IRemoteStorage
    {
        internal StorageClient Storage { get; }
        internal string BucketName { get; }

        public GoogleRemoteStorage(StorageClient storage, string bucketName)
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
}