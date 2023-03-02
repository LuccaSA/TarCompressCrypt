using Azure.Storage.Blobs;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TCC.Lib.Storage
{
    public class AzureRemoteStorage : IRemoteStorage
    {
        private readonly BlobContainerClient _container;

        public AzureRemoteStorage(BlobContainerClient container)
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

        public UploadMode Mode => UploadMode.AzureSdk;

    }
}