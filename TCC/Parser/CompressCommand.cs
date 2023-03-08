using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;
using TCC.Lib;
using TCC.Lib.Blocks;
using TCC.Lib.Database;
using TCC.Lib.Options;

namespace TCC.Parser
{

    public class CompressOptionBinding : CompressOption
    {
        public CompressOptionBinding()
        {
            base.Threads = 1;
        }

        public string Source { set => SourceDirOrFile = value; }
        public bool Individual { set => BlockMode = value ? BlockMode.Individual : BlockMode.Explicit; }

        public new string Threads { set => base.Threads = ParseCommandLineHelper.ThreadsParsing(value); }
        public string Password { set => ParseCommandLineHelper.ExtractInlinePassword(this, value); }
        public string PassFile { set => ParseCommandLineHelper.ExtractPasswordFile(this, value); }
        public string Key { set => ParseCommandLineHelper.ExtractAsymetricFile(this, Mode.Compress, value); }
    }

    public class CompressCommand : TccCommand<CompressOptionBinding>
    {
        public CompressCommand() : base("compress", "Compress specified files/folders")
        {
        }

        protected override IEnumerable<Argument> CreateArguments()
        {
            yield return new Argument<string> { Arity = ArgumentArity.ExactlyOne, Name = "source", Description = "Files or Folders to compress" };
        }

        protected override IEnumerable<Option> CreateOptions()
        {
            foreach (var option in BaseCmdOptions.CreateBaseOptions())
            {
                yield return option;
            }
            yield return new Option<bool>(new[] { "-i", "--individual" }, "Individual mode : create distinct archives for each file / folder in source");
            yield return new Option<CompressionAlgo>(new[] { "-a", "--algo", "--algorithm" }, description: "Algorithm : Lz4 (default), Brotli, Zstd", getDefaultValue: () => CompressionAlgo.Lz4);
            yield return new Option<int>(new[] { "-r", "--ratio", "--compression-ratio" }, "Compression ratio. Valid values are : lz4 [1,9], brotli [1,11], zstd [1,19] ");
            yield return new Option<BackupMode?>(new[] { "-m", "--mode" }, "Backup mode : Full or Diff. (Default = Full if no Diff, and Diff if Full already exists)\n" +
                                            "Full : force a full backup for all sources.\n" +
                                            "Diff : Archive delta since last full");
            yield return new Option<int?>(new[] { "--retries" }, "Number seconds retring a failed compression job");
            yield return new Option<IEnumerable<string>>(new[] { "--filter" }, "Optional filters");
            yield return new Option<IEnumerable<string>>(new[] { "--exclude" }, "Exclusion filters");
            yield return new Option<bool>(new[] { "--folderPerDay" }, "Creates a sub folder per day");
            yield return new Option<int>(new[] { "--maximumRetention" }, "Maximum retention in minutes");
            yield return new Option<int?>(new[] { "--boostRatio" }, "When more than 90% of Diff will be processed, allow to multiply the number of thread.\n"
                                             + "Ex : boostRatio 4 when having 8 thread will switch process on 32 threads");
            yield return new Option<int?>(new[] { "--cleanupTime" }, "Specify the time, in hours, after which the backup files are deleted. If no time is specified, then no backup files are deleted.");

            // Remote storage options
            foreach (var option in RemoteStorageOptions.GetCommonOptions())
            {
                yield return option;
            }
            yield return new Option<UploadMode?>(new[] { "--uploadMode" }, "Upload mode (retro compat with single upload options)");
            yield return new Option<IEnumerable<UploadMode>>(new[] { "--uploadModes" }, "Upload modes");
            yield return new Option<string>(new[] { "--s3MultipartThreshold" }, "Threshold from which sending a file is done with multi parts");
            yield return new Option<string>(new[] { "--s3MultipartSize" }, "If sending a file in multipart, the multipart size");
        }
        protected override Task RunAsync(ITccController controller, CompressOptionBinding option) => controller.CompressAsync(option);
    }
}