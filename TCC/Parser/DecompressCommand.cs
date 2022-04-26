using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;
using TCC.Lib.Options;

namespace TCC.Parser
{
    public class DecompressOptionBinding : DecompressOption
    {
        public DecompressOptionBinding()
        {
            base.Threads = 1;
        }
        public string Source { set => SourceDirOrFile = value; }
        public new string Threads { set => base.Threads = ParseCommandLineHelper.ThreadsParsing(value); }
        public string Password { set => ParseCommandLineHelper.ExtractInlinePassword(this, value); }
        public string PassFile { set => ParseCommandLineHelper.ExtractPasswordFile(this, value); }
        public string Key { set => ParseCommandLineHelper.ExtractAsymetricFile(this, Mode.Decompress, value); }
    }

    public class DecompressCommand : TccCommand<DecompressOptionBinding>
    {
        public DecompressCommand() : base("decompress", "Decompress specified files/folders")
        {
        }

        protected override IEnumerable<Argument> CreateArguments()
        {
            yield return new Argument<string> { Arity = ArgumentArity.ExactlyOne, Name = "source", Description = "Files or Folders to decompress" };
        }

        protected override IEnumerable<Option> CreateOptions() => BaseCmdOptions.CreateBaseOptions();

        protected override Task RunAsync(ITccController controller, DecompressOptionBinding option) => controller.DecompressAsync(option);
    }
}