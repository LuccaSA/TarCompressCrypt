using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TCC.Lib.Storage
{
    public class S3RemoteStorage : IRemoteStorage
    {
        internal string BucketName { get; }
        private readonly AmazonS3Client _s3Client;

        public S3RemoteStorage(AmazonS3Client s3Client, string bucketName)
        {
            BucketName = bucketName;
            _s3Client = s3Client;
        }

        public async Task<UploadResponse> UploadAsync(string targetPath, Stream data, CancellationToken token)
        {
            try
            {
                await _s3Client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest() {
                    BucketName = BucketName,
                    Key = targetPath,
                    InputStream = data,
                }, token);
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