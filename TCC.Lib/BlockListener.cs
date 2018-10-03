using System;
using System.Collections.Concurrent;
using System.Threading;
using TCC.Lib.Blocks;
using TCC.Lib.Command;

namespace TCC.Lib
{
    public class BlockListener
    {
        public BlockListener()
        {
            BlockingCollection = new BlockingCollection<(CommandResult Cmd, Block Block, int Total)>();
            ThreadPool.QueueUserWorkItem(_ => OnProgress(ref _counter, BlockingCollection));
        }

        private int _counter = 0;

        public BlockingCollection<(CommandResult Cmd, Block Block, int Total)> BlockingCollection { get; } 

        private void OnProgress(ref int counter, BlockingCollection<(CommandResult Cmd, Block Block, int Total)> blockingCollection)
        {
            foreach (var r in blockingCollection.GetConsumingEnumerable())
            {
                Interlocked.Increment(ref counter);
                Console.WriteLine(counter + "/" + r.Total + " : " + r.Block.ArchiveName);
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
    }
}