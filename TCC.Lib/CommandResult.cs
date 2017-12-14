using System;
using System.Collections.Generic;

namespace TCC
{
    public class CommandResult
    {
        public CommandResult()
        {
            Infos = new List<string>();
        }

        public int ExitCode { get; set; }
        public bool IsSuccess { get; set; }
        public string Output { get; set; }
        public string Errors { get; set; }
        public bool HasError => !String.IsNullOrEmpty(Errors);
        public string Command { get; set; }
        public Block Block { get; set; }
        public int BatchTotal { get; set; }

        public List<string> Infos { get; }
    }
}