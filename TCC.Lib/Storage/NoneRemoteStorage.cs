using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Options;

namespace TCC.Lib.Storage
{
    public class NoneRemoteStorage : IRemoteStorage
    {
        public Task<UploadResponse> UploadAsync(string targetPath, Stream data, CancellationToken token)
        {
            return Task.FromResult(new UploadResponse { IsSuccess = true, RemoteFilePath = targetPath });
        }

        public IAsyncEnumerable<(FileInfo fileInfos, string Key, long Size)> ListArchivesMatchingAsync(
            RetrieveOptions options,
            CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public UploadMode Mode => UploadMode.None;

        public Task DownloadAsync(string getRemoteStorageKey, string destinationPath,
            CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
