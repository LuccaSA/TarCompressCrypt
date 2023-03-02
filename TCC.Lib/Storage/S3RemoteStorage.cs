using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace TCC.Lib.Storage
{
    public class S3RemoteStorage : IRemoteStorage
    {
        internal string BucketName { get; }
        private readonly AmazonS3Client _s3Client;
        private readonly long _multipartTreshold;
        private readonly int _partSize;

        public S3RemoteStorage(AmazonS3Client s3Client, string bucketName, long multipartThreshold = 0, int partSize = 0)
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
                if (_multipartTreshold != 0  && data.Length > _multipartTreshold)
                {
                    await UploadStreamToMultiparts(targetPath, data, token);
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

        private async Task UploadStreamToMultiparts(string targetPath, Stream data, CancellationToken token)
        {
            
            var multipartUpload = await _s3Client.InitiateMultipartUploadAsync(new ()
            {
                BucketName = BucketName,
                Key = targetPath,
            }, token);
            var partsETags = new List<PartETag>();
            var partNumber = 1;

            await foreach (var chunk in ChunkStreamAsync(data, token))
            {
                chunk.Position = 0;
                var partUpload = await _s3Client.UploadPartAsync(new()
                {
                    BucketName = BucketName,
                    PartNumber = partNumber++,
                    Key = targetPath,
                    UploadId = multipartUpload.UploadId,
                    InputStream = chunk,
                }, token);

                partsETags.Add(new()
                {
                    ETag = partUpload.ETag,
                    PartNumber = partUpload.PartNumber,
                });
            }
            var response = await _s3Client.CompleteMultipartUploadAsync(new()
            {
                BucketName = BucketName,
                Key = targetPath,
                UploadId = multipartUpload.UploadId,
                PartETags = partsETags
            }, token);
        }
        private async IAsyncEnumerable<Stream> ChunkStreamAsync(Stream data, [EnumeratorCancellation] CancellationToken token)
        {
            var buffer = new byte[_partSize];
            int readBytes;

            do
            {
                readBytes = await data.ReadAsync(buffer, 0, _partSize, token);

                var notTheSame = new MemoryStream(readBytes);
                notTheSame.Write(buffer, 0, readBytes);
                
                yield return notTheSame;
            } while (readBytes >= _partSize);
        }

        public UploadMode GetMode() => UploadMode.S3;
    }
}