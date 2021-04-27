using System;
using System.IO;
using System.Text;
using TCC.Lib.Blocks;
using TCC.Lib.Command;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Dependencies
{
    public class UploadCommands
    {
        private readonly ExternalDependencies _ext;

        public UploadCommands(ExternalDependencies externalDependencies)
        {
            _ext = externalDependencies;
        }

        public string UploadCommand(CompressOption option, FileInfo file, DirectoryInfo root)
        {
            var fullPath = file.FullName;
            if (!fullPath.StartsWith(root.FullName))
            {
                throw new TccException("Incoherent file to upload");
            }
            string targetPath = file.GetRelativeTargetPathTo(root);

            var sb = new StringBuilder();
            sb.Append(_ext.AzCopy());
            sb.Append(" copy ");
            sb.Append(file.FullName.Escape());
            sb.Append(" ");
            string target = $"{option.AzBlobUrl}/{option.AzBlobContainer}/{targetPath}?{option.AzSaS}";
            sb.Append(target.Escape());

            if (option.AzMbps.HasValue)
            {
                sb.Append($" --cap-mbps {option.AzMbps.Value} ");
            }
            sb.Append(" --output-type json");

            return sb.ToString();
        }
    }
    
    public static class UploadHelper
    {
        public static string GetRelativeTargetPathTo(this FileInfo file, DirectoryInfo root )
        {
            var targetPath = file.FullName.Replace(root.FullName, string.Empty, StringComparison.InvariantCultureIgnoreCase)
                .Trim('\\').Trim('/');

            targetPath = targetPath.Replace('\\', '/');

            return targetPath;
        }
    }


    public class AzCopyResponse
    {
        public DateTime TimeStamp { get; set; }
        public string MessageType { get; set; }
        public string MessageContent { get; set; }
        public AzCopyDetails PromptDetails { get; set; }
    }

    public class AzCopyDetails
    {
        public string PromptType { get; set; }
        public object ResponseOptions { get; set; }
        public string PromptTarget { get; set; }
    }


    public class AzCopyJobCompleted
    {
        public string ErrorMsg { get; set; }
        public string JobID { get; set; }
        public string ActiveConnections { get; set; }
        public bool CompleteJobOrdered { get; set; }
        public string JobStatus { get; set; }
        public string TotalTransfers { get; set; }
        public string FileTransfers { get; set; }
        public string FolderPropertyTransfers { get; set; }
        public string TransfersCompleted { get; set; }
        public string TransfersFailed { get; set; }
        public string TransfersSkipped { get; set; }
        public string BytesOverWire { get; set; }
        public string TotalBytesTransferred { get; set; }
        public string TotalBytesEnumerated { get; set; }
        public string TotalBytesExpected { get; set; }
        public string PercentComplete { get; set; }
        public string AverageIOPS { get; set; }
        public string AverageE2EMilliseconds { get; set; }
        public string ServerBusyPercentage { get; set; }
        public string NetworkErrorPercentage { get; set; }
        public object FailedTransfers { get; set; }
        public object SkippedTransfers { get; set; }
        public int PerfConstraint { get; set; }
        public object[] PerformanceAdvice { get; set; }
        public bool IsCleanupJob { get; set; }
    }


    public class AzCopyProcessOutputClassifier : IProcessOutputClassifier
    {
        public bool IsIgnorable(string line) => false;
        public bool IsInfo(string line) => true;
    }
}