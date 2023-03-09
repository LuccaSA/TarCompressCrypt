using Google.Type;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Globalization;
using System.Threading.Tasks;
using TCC.Lib;
using TCC.Lib.Database;
using TCC.Lib.Options;
using DateTime = System.DateTime;

namespace TCC.Parser
{
    public class RetrieveOptionBinding : RetrieveOptions
    {
        public RetrieveOptionBinding()
        {
            base.Threads = 1;
        }
        public string Source { set => SourceArchive = value; }
        public new string Threads { set => base.Threads = ParseCommandLineHelper.ThreadsParsing(value); }
        public string Password { set => ParseCommandLineHelper.ExtractInlinePassword(this, value); }
        public string PassFile { set => ParseCommandLineHelper.ExtractPasswordFile(this, value); }
        public string Key { set => ParseCommandLineHelper.ExtractAsymetricFile(this, TCC.Lib.Options.Mode.Decompress, value); }
        public string MachineName { set => SourceMachine = value; }
        public string Before
        {
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    BeforeDateTime = DateTime.UtcNow.AddDays(1);
                }
                else
                {
                    BeforeDateTime = DateTime.Parse(value, CultureInfo.InvariantCulture);
                }
            }
        }
    }

    public class RetrieveCommand : TccCommand<RetrieveOptionBinding>
    {
        public RetrieveCommand() : base("retrieve", "Retrieve specified files/folders from remote storage and decompress them")
        {
        }

        protected override IEnumerable<Argument> CreateArguments()
        {
            yield return new Argument<string> { Arity = ArgumentArity.ExactlyOne, Name = "machineName", Description = "Name of the machine from which retrieve the source" };
            yield return new Argument<string> { Arity = ArgumentArity.ZeroOrOne, Name = "source", Description = "Name of the source archive to retrieve" };
        }

        protected override IEnumerable<Option> CreateOptions()
        {
            foreach (var option in BaseCmdOptions.CreateBaseOptions())
            {
                yield return option;
            }
            // Remote storage options
            foreach (var option in RemoteStorageOptions.GetCommonOptions())
            {
                yield return option;
            }
            yield return new Option<UploadMode>(new[] { "--downloadMode" }, "Download mode");
            yield return new Option<BackupMode>(new[] { "--mode" }, "Backup mode : Full or Diff. Default: Diff. Diff including Full backups");
            yield return new Option<string>(new[] { "--before" }, "Download last backup before specified date (format: YYYY-MM-DD)");
            yield return new Option<bool>(new[] { "--all" }, "Retrieve full backup history (default: false)");
            yield return new Option<bool>(new[] { "--folderPerDay" }, "To use is the archive was created with --folderPerDay option");
        }

        protected override Task RunAsync(ITccController controller, RetrieveOptionBinding option) => controller.RetrieveAsync(option);
    }
}