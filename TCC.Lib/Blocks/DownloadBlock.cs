using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Storage;

namespace TCC.Lib.Blocks
{
    public class DownloadBlock : DecompressionBlock
    {
        public long RemoteStorageSize { get; init; }
        public override long CompressedSize => RemoteStorageSize;

        private string GetDestinationPath(DirectoryInfo destinationDir) =>
            Path.Combine(destinationDir.FullName, this.GetRemoteStorageKey(destinationDir));

        public async Task DownloadAsync(DirectoryInfo destinationDir, IRemoteStorage downloader, CancellationToken token)
        {
            var dstPath = GetDestinationPath(destinationDir);
            if (!File.Exists(dstPath) || new FileInfo(dstPath).Length != SourceArchiveFileInfo.Length)
            {
                await downloader.DownloadAsync(this.GetRemoteStorageKey(destinationDir), dstPath, token);
            }
        }
    }
}