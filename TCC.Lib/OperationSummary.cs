using System.Collections.Generic;
using TCC.Lib.Blocks;
using TCC.Lib.Command;

namespace TCC.Lib
{
    public class OperationSummary
    {
        public OperationSummary(IEnumerable<Block> blocks, IEnumerable<CommandResult> commandResults, bool isSuccess)
        {
            Blocks = blocks;
            CommandResults = commandResults;
            IsSuccess = isSuccess;
        }

        public IEnumerable<Block> Blocks { get; }
        public IEnumerable<CommandResult> CommandResults { get; }
        public bool IsSuccess { get; }
    }
}