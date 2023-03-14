using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Dependencies;
using TCC.Lib.Options;

namespace TCC.Lib.Storage
{
    public interface IRemoteStorage
    {
        Task<UploadResponse> UploadAsync(string targetPath, Stream data, CancellationToken token);

        async Task<UploadResponse> UploadAsync(FileInfo file, DirectoryInfo rootFolder, CancellationToken token)
        {
            string targetPath = file.GetRelativeTargetPathTo(rootFolder);
            await using FileStream uploadFileStream = File.OpenRead(file.FullName);
            return await UploadAsync(targetPath, uploadFileStream, token);
        }
        IAsyncEnumerable<(FileInfo fileInfos, string Key, long Size)> ListArchivesMatchingAsync(
            RetrieveOptions options,
            CancellationToken token);
        UploadMode Mode { get; }
        Task DownloadAsync(string getRemoteStorageKey, string destinationPath,
            CancellationToken token);
    }
}