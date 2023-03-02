using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TCC.Lib.Storage
{
    public class NoneRemoteStorage : IRemoteStorage
    {
        public Task<UploadResponse> UploadAsync(string targetPath, Stream data, CancellationToken token)
        {
            return Task.FromResult(new UploadResponse { IsSuccess = true, RemoteFilePath = targetPath });
        }

        public UploadMode Mode => UploadMode.None;
    }
}
