using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;
using TCC.Lib.Options;

namespace TCC.Parser
{
    public class AutoDecompressOptionBinding : DecompressOption
    {
        public AutoDecompressOptionBinding()
        {
            base.Threads = 1;
        }
        public new string Threads { set => base.Threads = ParseCommandLineHelper.ThreadsParsing(value); }
        public string Password { set => ParseCommandLineHelper.ExtractInlinePassword(this, value); }
        public string PassFile { set => ParseCommandLineHelper.ExtractPasswordFile(this, value); }
        public string Key { set => ParseCommandLineHelper.ExtractAsymetricFile(this, Mode.Decompress, value); }
        public string GoogleStorageCredential { get; set; }
        public string GoogleProjectId { get; set; }
        public string GoogleSubscriptionId { get; set; }
        public string TemporaryDirectory { get; set; }
    }


    public class AutoDecompressCommand : TccCommand<AutoDecompressOptionBinding>
    {
        public AutoDecompressCommand() : base("auto-decompress", "Continuous decompress from Google Cloud Storage")
        {
        }

        protected override IEnumerable<Argument> CreateArguments()
        {
            yield break;
        }
        protected override IEnumerable<Option> CreateOptions()
        {
            foreach (var option in BaseCmdOptions.CreateBaseOptions())
            {
                yield return option;
            }
            yield return new Option<string>(new[] { "--googleStorageCredential" }, "Google Cloud Storage credential json, either full path or base64");
            yield return new Option<string>(new[] { "--googleProjectId" }, "Google Cloud Pub/Sub, storage projectId");
            yield return new Option<string>(new[] { "--googleSubscriptionId" }, "Google Cloud Pub/Sub subscriptionId");
            yield return new Option<string>(new[] { "--temporaryDirectory" }, "Directory where archive are downloaded temporary");
        }

        protected override Task RunAsync(ITccController controller, AutoDecompressOptionBinding option)
            => controller.AutoDecompressAsync(option);
    }
}
