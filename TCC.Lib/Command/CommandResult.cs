using System;
using System.Collections.Generic;
using TCC.Lib.Blocks;

namespace TCC.Lib.Command
{
    public class CommandResult
    {
        public int ExitCode { get; set; }
        public bool IsSuccess { get; set; }
        public string Output { get; set; }
        public string Errors { get; set; }
        public bool HasError => !String.IsNullOrEmpty(Errors);
        public string Command { get; set; }
        public List<string> Infos { get; set; }
    }
}