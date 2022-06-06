using System.Collections.Generic;
using System.CommandLine;

namespace TCC.Parser
{
    public static class BaseCmdOptions
    {
        public static IEnumerable<Option> CreateBaseOptions()
        {
            yield return new Option<string>(new[] { "-o", "--output", "--destination-dir" }, description: "Output directory path") { IsRequired = true };
            var threadOption = new Option<string>(new[] { "-t", "--threads" }, "Number of threads [1,2...N] / all");
            threadOption
                .AddValidator(result =>
                {
                    string threads = result.GetValueForOption(threadOption);
                    if (!(
                        string.IsNullOrEmpty(threads) ||
                        string.Equals(threads, "all", System.StringComparison.InvariantCultureIgnoreCase) ||
                        int.TryParse(threads, out _)
                        ))
                    {
                        result.ErrorMessage = "Maximum threads need to be either numeric, or \"all\"";
                    }
                });
            yield return threadOption;
            yield return new Option<bool>(new[] { "-f", "--failFast" }, "Fail-fast mode");
            yield return new Option<bool>(new[] { "--ignore-missing-full" }, "Still decompress DIFF when FULL is missing");
            yield return new Option<string>(new[] { "-p", "--password" }, "encryption password");
            yield return new Option<string>(new[] { "-e", "--passFile" }, "file with password on one line");
            yield return new Option<string>(new[] { "-k", "--key" }, "Public key for compression, private key for decompression");
            yield return new Option<string>(new[] { "-s", "--slackSecret" }, "Slack xoxp Secret");
            yield return new Option<string>(new[] { "-c", "--slackChannel" }, "Slack #channel name");
            yield return new Option<string>(new[] { "-b", "--bucketName" }, "Slack notification bucket name");
            yield return new Option<bool>(new[] { "--slackOnlyOnError" }, "Send slack message only on warning or error");
        }
    }
}