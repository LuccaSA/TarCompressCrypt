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

                if (r.Block is CompressionBlock cb)
                {
                    var report = $"{count}/{r.Total} : {cb.BlockName} [{cb.BackupMode ?? BackupMode.Full}]";
                    if (cb.BackupMode == BackupMode.Diff)
                    {
                        report += $" from {cb.DiffDate}";
                    }
                    Console.WriteLine(report);
                }
                else if(r.Block is DecompressionBlock db)
                {
                    var report = $"{count}/{r.Total} : {db.BlockName}";
                    Console.WriteLine(report);
                }
               
                if (r.Cmd.HasError)
                {
                    Console.Error.WriteLine("Error : " + r.Cmd.Errors);
                }

                if (!String.IsNullOrEmpty(r.Cmd.Output))
                {
                    Console.Out.WriteLine("Info : " + r.Cmd.Errors);
                }
            }
        }
        
        public void OnBlockReport(BlockReport report)
        {
            _blockingCollection.Add(report);
        }
    }
}