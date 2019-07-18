using System;
using TCC.Lib.Command;
using System.Linq;
using System.Text;

namespace TCC.Lib.Blocks
{
    public abstract class BlockReport
    {
        public BlockReport(int blocksCount)
        {
            Total = blocksCount;
        }


        public int Total { get; }
        public abstract bool HasError { get; }
        public abstract string Output { get; }
        public abstract string Infos { get; }
        public abstract string Errors { get; }
    }

    public class CompressionBlockReport : BlockReport
    {
        public CompressionBlockReport(CommandResult result, CompressionBlock block, int blocksCount)
            : base(blocksCount)
        {
            Cmd = result;
            CompressionBlock = block;
        }
        public CommandResult Cmd { get; }
        public CompressionBlock CompressionBlock { get; }
        public override bool HasError => Cmd.HasError;
        public override string Output => Cmd.Output;
        public override string Infos => string.Join(Environment.NewLine, Cmd.Infos);
        public override string Errors => Cmd.Errors;
    }

    public class DecompressionBlockReport : BlockReport
    {
        public DecompressionBlockReport(DecompressionBatch block, int blocksCount)
            : base(blocksCount)
        {
            DecompressionBatch = block ?? throw new ArgumentNullException(nameof(block));
        }

        public DecompressionBatch DecompressionBatch { get; }

        public override bool HasError =>
            (DecompressionBatch?.BackupFullCommandResult?.HasError ?? false)
            || (DecompressionBatch?.BackupDiffCommandResult?.Any(i => i.HasError) ?? false);

        public override string Output
        {
            get
            {
                var sb = new StringBuilder();
                if (!String.IsNullOrWhiteSpace(DecompressionBatch?.BackupFullCommandResult?.Output))
                {
                    sb.AppendLine(DecompressionBatch.BackupFullCommandResult.Output);
                }
                if (DecompressionBatch?.BackupDiffCommandResult != null)
                {
                    foreach (var cr in DecompressionBatch.BackupDiffCommandResult
                        .Where(i => !String.IsNullOrWhiteSpace(i.Output)))
                    {
                        sb.AppendLine(cr.Output);
                    }
                }
                return sb.ToString();
            }
        }

        public override string Infos
        {
            get
            {
                var sb = new StringBuilder();
                if (DecompressionBatch?.BackupFullCommandResult?.Infos.Any() ?? false)
                {
                    sb.AppendLine(string.Join(Environment.NewLine, DecompressionBatch.BackupFullCommandResult.Infos));
                }
                if (DecompressionBatch?.BackupDiffCommandResult != null)
                {
                    foreach (var cr in DecompressionBatch.BackupDiffCommandResult.Where(i => i.Infos.Any()))
                    {
                        sb.AppendLine(string.Join(Environment.NewLine, cr.Infos));
                    }
                }
                return sb.ToString(); 
            }
        }

        public override string Errors
        {
            get
            {
                var sb = new StringBuilder();
                if (!String.IsNullOrWhiteSpace(DecompressionBatch?.BackupFullCommandResult?.Errors))
                {
                    sb.AppendLine(DecompressionBatch.BackupFullCommandResult.Errors);
                }
                if (DecompressionBatch?.BackupDiffCommandResult != null)
                {
                    foreach (var cr in DecompressionBatch.BackupDiffCommandResult
                        .Where(i => !String.IsNullOrWhiteSpace(i.Errors)))
                    {
                        sb.AppendLine(cr.Errors);
                    }
                }
                return sb.ToString();
            }
        }
    }
}
