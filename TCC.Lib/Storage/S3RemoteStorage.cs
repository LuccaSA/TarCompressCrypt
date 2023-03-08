using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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

        public Task<UploadResponse> DownloadAsync()
        {
            throw new NotImplementedException();
        }

        public UploadMode Mode => UploadMode.S3;
    }
}