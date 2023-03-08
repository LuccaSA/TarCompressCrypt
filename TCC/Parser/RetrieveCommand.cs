using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;
using TCC.Lib;
using TCC.Lib.Options;

namespace TCC.Parser
{
    public class RetrieveOptionBinding : RetrieveOptions
    {
    }

    public class RetrieveCommand : TccCommand<RetrieveOptionBinding>
    {
        public RetrieveCommand() : base("retrieve", "Retrieve specified files/folders from remote storage and decompress them")
        {
        }

        protected override IEnumerable<Argument> CreateArguments()
        {
            yield return new Argument<string> { Arity = ArgumentArity.ExactlyOne, Name = "machineName", Description = "Name of the machine from which retrieve the source" };
            yield return new Argument<string> { Arity = ArgumentArity.ExactlyOne, Name = "source", Description = "Name of the source archive to retrieve" };
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

        }

        protected override Task RunAsync(ITccController controller, RetrieveOptionBinding option) => controller.RetrieveAsync(option);
    }
}