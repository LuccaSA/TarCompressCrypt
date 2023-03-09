﻿using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Database;
using TCC.Lib.Options;

namespace TCC.Lib.Storage
{
    public class S3RemoteStorage : IRemoteStorage
    {
        internal string BucketName { get; }
        private readonly AmazonS3Client _s3Client;
        private readonly long _multipartTreshold;
        private readonly long _partSize;

        public S3RemoteStorage(AmazonS3Client s3Client, string bucketName, long multipartThreshold = 0, long partSize = 0)
        {
            BucketName = bucketName;
            _s3Client = s3Client;
            _multipartTreshold = multipartThreshold;
            _partSize = partSize;
        }

        public async Task<UploadResponse> UploadAsync(string targetPath, Stream data, CancellationToken token)
        {
            try
            {
                if (_multipartTreshold > 0 && data.Length > _multipartTreshold)
                {
                    await UploadStreamToMultipartsAsync(targetPath, data, token);
                } else
                {
                    await _s3Client.PutObjectAsync(new ()
                    {
                        BucketName = BucketName,
                        Key = targetPath,
                        InputStream = data,
                    }, token);
                }
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

        private async Task UploadStreamToMultipartsAsync(string targetPath, Stream data, CancellationToken token)
        {
            var multipartUpload = await _s3Client.InitiateMultipartUploadAsync(new ()
            {
                BucketName = BucketName,
                Key = targetPath,
            }, token);
            var partsETags = new List<PartETag>();
            var partNumber = 1;

            
            while (true)
            {
                await using var chunk = new ReadOnlyChunkedStream(data, _partSize);
                
                if (!chunk.CanRead || chunk.Length == 0)
                {
                    break;
                }

                var partUpload = await _s3Client.UploadPartAsync(new()
                {
                    BucketName = BucketName,
                    PartNumber = partNumber++,
                    Key = targetPath,
                    UploadId = multipartUpload.UploadId,
                    InputStream = chunk,
                }, token);

                partsETags.Add(new() {ETag = partUpload.ETag, PartNumber = partUpload.PartNumber,});
            }
            await _s3Client.CompleteMultipartUploadAsync(new()
            {
                BucketName = BucketName,
                Key = targetPath,
                UploadId = multipartUpload.UploadId,
                PartETags = partsETags
            }, token);
        }

        public async IAsyncEnumerable<(string Key, long Size)> ListArchivesMatchingWithSizeAsync(
            RetrieveOptions options,
            [EnumeratorCancellation] CancellationToken token)
        {
            var keyList = ListAllAsync(token).Where(obj =>
            {
                if (!string.IsNullOrWhiteSpace(options.SourceArchive) && !obj.Key.Contains(options.SourceArchive))
                {
                    return false;
                }

                if (options.Mode == BackupMode.Full && !obj.Key.Contains("FULL"))
                {
                    return false;
                }
                
                if (obj.LastModified > options.BeforeDateTime)
                {
                    return false;
                }
                return true;
            });
            
            //TODO If all is false, we should return the latest one (with it's diff if needed)

            await foreach (var obj in keyList.WithCancellation(token))
            {
                yield return (obj.Key, obj.Size);
            }
        }

        private async IAsyncEnumerable<S3Object> ListAllAsync([EnumeratorCancellation] CancellationToken token)
        {
            ListObjectsV2Response s3Response;
            ListObjectsV2Request s3Request = new() {BucketName = BucketName};
            do
            {
                s3Response = await _s3Client.ListObjectsV2Async(s3Request, token);
                foreach (S3Object s3Object in s3Response.S3Objects)
                {
                    yield return s3Object;
                }
                s3Request.ContinuationToken = s3Response.NextContinuationToken;
            } while (s3Response.IsTruncated);
        }

        public UploadMode Mode => UploadMode.S3;

        public async Task DownloadAsync(string remoteStorageKey, DirectoryInfo destinationDir,
            CancellationToken token)
        {
            try
            {
                var request = new GetObjectRequest() {BucketName = BucketName, Key = remoteStorageKey.Replace(Path.DirectorySeparatorChar, '/'),};
                var dstPath = Path.Combine(destinationDir.FullName, remoteStorageKey);
                var parentFolder = Path.GetDirectoryName(dstPath);
                if (!Directory.Exists(parentFolder) && parentFolder is not null)
                {
                    Directory.CreateDirectory(parentFolder);
                }
                using (var objectResponse = await _s3Client.GetObjectAsync(request, token))
                using (var responseStream = objectResponse.ResponseStream)
                using (var file = File.Create(dstPath))
                {
                    await responseStream.CopyToAsync(file, token);
                }
            } catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                throw new Exception($"Error while downloading {remoteStorageKey} from S3", e);
            }
        }
    }
}