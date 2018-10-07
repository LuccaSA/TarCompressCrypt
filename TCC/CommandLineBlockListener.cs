using System;
using System.Collections.Concurrent;
using System.Threading;
using TCC.Lib.Blocks;

namespace TCC
{
    public class CommandLineBlockListener : IBlockListener
    {
        public CommandLineBlockListener()
        {
            BlockingCollection = new BlockingCollection<BlockReport>();
            ThreadPool.QueueUserWorkItem(_ => OnProgress(ref _counter, BlockingCollection));
        }

        private int _counter;

        public BlockingCollection<BlockReport> BlockingCollection { get; } 

        private void OnProgress(ref int counter, BlockingCollection<BlockReport> blockingCollection)
        {
            foreach (var r in blockingCollection.GetConsumingEnumerable())
            {
                int count = Interlocked.Increment(ref counter);
                Console.WriteLine(count + "/" + r.Total + " : " + r.Block.ArchiveName);
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

        public void Add(BlockReport report)
        {
            BlockingCollection.Add(report);
        }
    }
}