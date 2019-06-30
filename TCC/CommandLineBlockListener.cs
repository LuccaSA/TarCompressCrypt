using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TCC.Lib.Blocks;
using TCC.Lib.Database;

namespace TCC
{
    public class CommandLineBlockListener : IBlockListener
    {
        public CommandLineBlockListener(ILogger<CommandLineBlockListener> logger)
        {
            _logger = logger;
            _reports = Channel.CreateUnbounded<BlockReport>();
            Task.Run(OnBlockReportAsync);
            _tcs = new TaskCompletionSource<bool>();
        }

        private int _counter;
        private readonly Channel<BlockReport> _reports;
        private readonly TaskCompletionSource<bool> _tcs;
        private readonly ILogger<CommandLineBlockListener> _logger;

        private async Task OnBlockReportAsync()
        {
            try
            {
                while (await _reports.Reader.WaitToReadAsync())
                {
                    while (_reports.Reader.TryRead(out var report))
                    {
                        ReportOnConsole(report);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Error while reporting on console");
            }
            _tcs.SetResult(true);
        }

        private void ReportOnConsole(BlockReport r)
        {
            int count = Interlocked.Increment(ref _counter);

            if (r is CompressionBlockReport cb)
            {
                var report = $"{count}/{r.Total} [{cb.CompressionBlock.BackupMode ?? BackupMode.Full}] : {cb.CompressionBlock.BlockName}";
                if (cb.CompressionBlock.BackupMode == BackupMode.Diff)
                {
                    report += $"(from {cb.CompressionBlock.DiffDate})";
                }

                Console.WriteLine(report);
            }
            else if (r is DecompressionBlockReport db)
            {
                string progress = $"{count}/{r.Total}";
                if (db.DecompressionBatch.BackupFull != null)
                {
                    var report = $"{progress} [{BackupMode.Full}] : {db.DecompressionBatch.BackupFull.BlockName} (from {db.DecompressionBatch.BackupFull.BlockDate})";
                    Console.WriteLine(report);
                }
                if (db.DecompressionBatch.BackupsDiff != null)
                {
                    foreach (var dec in db.DecompressionBatch.BackupsDiff)
                    {
                        var report = $"{progress} [{BackupMode.Diff}] : {dec.BlockName} (from {dec.BlockDate})";
                        Console.WriteLine(report);
                    }
                }
            }

            if (r.HasError)
            {
                Console.Error.WriteLine("Error : " + r.Errors);
            }

            if (!String.IsNullOrEmpty(r.Output))
            {
                Console.Out.WriteLine("Info : " + r.Errors);
            }
        }

        public async Task OnCompressionBlockReportAsync(CompressionBlockReport report)
        {
            await _reports.Writer.WriteAsync(report);
        }

        public async Task OnDecompressionBatchReportAsync(DecompressionBlockReport report)
        {
            await _reports.Writer.WriteAsync(report);
        }

        public Task CompletedReports => _tcs.Task;
        public void Complete()
        {
            _reports.Writer.Complete();
        }
    }
}