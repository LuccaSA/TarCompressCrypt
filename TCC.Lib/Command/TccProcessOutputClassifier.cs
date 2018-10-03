using System.Collections.Generic;
using System.Linq;

namespace TCC.Lib.Command
{
    public class TccProcessOutputClassifier : IProcessOutputClassifier
    {
        public static TccProcessOutputClassifier Instance { get; } = new TccProcessOutputClassifier();

        /// <summary>
        /// Filter "good" stderror as infos
        /// </summary>
        /// <returns></returns>
        public bool IsInfo(string line)
        {
            if (line.StartsWith("Compressed"))
            {
                return true;
            }

            if (line.EndsWith(" bytes "))
            {
                return true;
            }

            if (line.EndsWith("MB...     ")) // zstd glitch
            {
                return true;
            }
            return false;
        }

        public bool IsIgnorable(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return true;
            }

            if (line.StartsWith("*** LZ4 command line interface"))
            {
                return true;
            }
            if (_opensslInfos.Contains(line) || IsOpenSslProgress(line))
            {
                return true;
            }
            return false;
        }

        private bool IsOpenSslProgress(string output)
        {
            return output.All(t => t == '.' || t == '+');
        }

        private static readonly HashSet<string> _opensslInfos = new HashSet<string>()
        {
            "*** WARNING : deprecated key derivation used.",
            "Using -iter or -pbkdf2 would be better.",
            "writing RSA key"
        };

    }
}