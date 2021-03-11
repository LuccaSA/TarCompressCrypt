using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TCC.Lib.Command
{
    public class CommandResult
    {
        public int ExitCode { get; set; }
        public bool IsSuccess { get; set; }
        public string Output { get; set; }
        public string Errors { get; set; }
        public bool HasError => !String.IsNullOrWhiteSpace(Errors);
        public bool HasWarning => Infos != null && Infos.Count != 0 && Infos.Any(i => !string.IsNullOrWhiteSpace(i));
        public string Command { get; set; }
        public List<string> Infos { get; set; }
        public long ElapsedMilliseconds => (long)Elapsed.TotalMilliseconds;
        public TimeSpan Elapsed { get; set; }

        public void ThrowOnError()
        {
            if (HasError)
            {
                var sb = new StringBuilder();
                sb.AppendLine("command : " + Command);
                sb.AppendLine("error : " + Errors);
                throw new TccException(sb.ToString());
            }
        }
    }
}