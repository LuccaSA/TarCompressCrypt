using System.Collections.Generic;
using System.Linq;
using TCC.Lib.Blocks;
using TCC.Lib.Command;

namespace TCC.Lib
{
    public class OperationSummary
    {
        public OperationSummary(IEnumerable<Block> blocks, IEnumerable<CommandResult> commandResults)
        {
            Blocks = blocks;
            CommandResults = commandResults;
        }

        public IEnumerable<Block> Blocks { get; }
        public IEnumerable<CommandResult> CommandResults { get; }
        public bool IsSuccess => CommandResults.All(c => c.IsSuccess);
    }
}