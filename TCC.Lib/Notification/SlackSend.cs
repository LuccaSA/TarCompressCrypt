using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TCC.Lib.Database;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Notification
{
    public class SlackSender
    {
        private readonly SlackClient _slackClient;

        public SlackSender(SlackClient slackClient)
        {
            _slackClient = slackClient;
        }

        public async Task ReportAsync(OperationSummary op, TccOption parsedOption, Mode mode)
        {
            if (string.IsNullOrWhiteSpace(parsedOption?.SlackSecret) ||
                string.IsNullOrWhiteSpace(parsedOption.SlackChannel))
            {
                return;
            }

            if (parsedOption.SlackOnlyOnError && !op.OperationBlocks
                .Any(i => i.HasWarning || i.HasError))
            {
                return;
            }

            string shell = SlackShell(op);
            string blocksStats = BlocksStats(op, mode);
            string bucket = parsedOption.BucketName ?? Environment.MachineName;

            var msgRoot = new SlackMessage
            {
                Channel = parsedOption.SlackChannel,
                Text = $"{shell} *{bucket}* : {mode} {blocksStats} in {op.Stopwatch.Elapsed.HumanizedTimeSpan()}",
            };
            var response = await _slackClient.SendSlackMessageAsync(msgRoot, parsedOption.SlackSecret);

            var msgDetail = new SlackMessage
            {
                Channel = parsedOption.SlackChannel,
                Text = $"*{mode}* Details on {bucket}",
                ThreadTs = response.Ts,
                Attachments = new List<Attachment>()
            };

            SlackReportDetail(op, parsedOption, msgDetail);
            await _slackClient.SendSlackMessageAsync(msgDetail, parsedOption.SlackSecret);
        }

        private static string BlocksStats(OperationSummary op, Mode mode)
        {
            string blocksStats;
            switch (mode)
            {
                case Mode.Compress:
                    blocksStats = string.Join(' ', op.OperationBlocks.OfType<OperationCompressionBlock>()
                        .GroupBy(i => i.CompressionBlock.BackupMode)
                        .Select(i => new { Mode = i.Key, Count = i.Count() })
                        .OrderBy(i => i.Mode).Select(i => $"{i.Count} {i.Mode}"));
                    break;
                case Mode.Decompress:
                    blocksStats = $"{op.OperationBlocks.Count()} blocks";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
            return blocksStats;
        }

        private static string SlackShell(OperationSummary op)
        {
            string shell = ":greenshell:";
            if (!op.OperationBlocks.Any())
            {
                shell = ":redshell:";
            }
            else
            {
                foreach (var result in op.OperationBlocks.SelectMany(o=> o.StepResults))
                {
                    if (result.HasError)
                    {
                        shell = ":redshell:";
                        break;
                    }

                    if (result.HasWarning)
                    {
                        shell = ":warning:";
                    }
                }
            }

            return shell;
        }

        private static void SlackReportDetail(OperationSummary op, TccOption parsedOption, SlackMessage msg)
        {
            var reports = new List<SlackReport>();
            foreach (var o in op.OperationBlocks)
            {
                ExtractSlackReports(o, reports);
            }

            foreach (var ga in reports.GroupBy(i => i.Alert))
            {
                foreach (var grp in ga.GroupBy(g => g.Message))
                {
                    msg.Attachments.Add(new Attachment
                    {
                        Color = ga.Key.ToSlackColor(),
                        Title = grp.Key,
                        Fields = grp.Select(d => new Field
                        {
                            Value = d.BlockName,
                            Short = true
                        }).ToArray()
                    });
                }
            }

            if (op.OperationBlocks.Any())
            {
                msg.Attachments.Add(new Attachment
                {
                    Color = AlertLevel.Info.ToSlackColor(),
                    Title = $"Statistics",
                    Fields = new[]
                    {
                        new Field
                        {
                            Value = $"{op.OperationBlocks.Count()} blocks in {op.Stopwatch.Elapsed.HumanizedTimeSpan()}",
                            Short = true
                        },
                        new Field
                        {
                            Value = $@"Job size : {op.OperationBlocks.Sum(i=>i.CompressedSize).HumanizeSize()}",
                            Short = true
                        },
                        new Field
                        {
                            Value = parsedOption.SourceDirOrFile, Short = true
                        },

                        new Field
                        {
                            Value = $"File throughput : {op.Statistics.AverageThroughput.HumanizedBandwidth()}", Short = true
                        }
                    }
                });
            }
            else
            {
                msg.Attachments.Add(new Attachment
                {
                    Color = AlertLevel.Error.ToSlackColor(),
                    Title = "Nothing processed !!!"
                });
            }
        }

        private static void ExtractSlackReports(IIterationResult block, List<SlackReport> reports)
        {

            foreach (var k in block.StepResults.Where(i => i.HasError))
            {
                reports.Add(new SlackReport
                {
                    BlockName = $"{k.Type} {k.Name}",
                    Message = k.Errors,
                    Alert = AlertLevel.Error
                });
            }

            foreach (var k in block.StepResults.Where(i => i.HasWarning))
            {
                reports.Add(new SlackReport
                {
                    BlockName = $"{k.Type} {k.Name}",
                    Message = k.Warning,
                    Alert = AlertLevel.Warning
                });
            }
        }

    }


    public class SlackReport
    {
        public string BlockName { get; set; }
        public string Message { get; set; }
        public AlertLevel Alert { get; set; }
    }
}
