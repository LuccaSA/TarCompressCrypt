using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Dependencies;

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
        Task<UploadResponse> DownloadAsync();
        UploadMode Mode { get; }
    }
}