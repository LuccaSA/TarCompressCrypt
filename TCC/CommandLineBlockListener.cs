using System;
using System.Collections.Concurrent;
using System.Threading;
using TCC.Lib.Blocks;
using TCC.Lib.Database;

namespace TCC
{
    public class CommandLineBlockListener : IBlockListener
    {
        public CommandLineBlockListener()
        {
            _blockingCollection = new BlockingCollection<BlockReport>();
            ThreadPool.QueueUserWorkItem(_ => OnProgress(ref _counter, _blockingCollection));
        }

        private int _counter;
        private readonly BlockingCollection<BlockReport> _blockingCollection;

        private void OnProgress(ref int counter, BlockingCollection<BlockReport> blockingCollection)
        {
            foreach (var r in blockingCollection.GetConsumingEnumerable())
            {
                int count = Interlocked.Increment(ref counter);

                if (r is CompressionBlockReport cb)
                {
                    var report = $"{count}/{r.Total} : {cb.CompressionBlock.BlockName} [{cb.CompressionBlock.BackupMode ?? BackupMode.Full}]";
                    if (cb.CompressionBlock.BackupMode == BackupMode.Diff)
                    {
                        report += $"(from {cb.CompressionBlock.DiffDate})";
                    }
                    Console.WriteLine(report);
                }
                else if(r is DecompressionBlockReport db)
                {
                    string progress = $"{count}/{r.Total} :";
                    if (db.DecompressionBatch.BackupFull != null)
                    {
                        var report = $"{progress} {db.DecompressionBatch.BackupFull.BlockName} [{BackupMode.Full}]";
                        Console.WriteLine(report);
                    }
                    else
                    {
                        if (db.DecompressionBatch.BackupsDiff != null)
                        {
                            foreach (var dec in db.DecompressionBatch.BackupsDiff)
                            {
                                var report = $"{progress} {dec.BlockName} [{BackupMode.Full}]";
                                Console.WriteLine(report);
                            }
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
        }
        
        public void OnBlockReport(BlockReport report)
        {
            _blockingCollection.Add(report);
        }

        public void OnCompressionBlockReport(CompressionBlockReport report)
        {
            _blockingCollection.Add(report);
        }

        public void OnDecompressionBatchReport(DecompressionBlockReport report)
        {
            _blockingCollection.Add(report);
        }
    }
}