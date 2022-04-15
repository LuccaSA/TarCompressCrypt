using TCC.Lib;
using System;
using TCC.Lib.Options;
using System.IO;

namespace TCC.Parser
{
    public static class ParseCommandLineHelper
    {
        public static int ThreadsParsing(string thread)
        {
            if (string.IsNullOrEmpty(thread))
            {
                return 1;
            }
            if (string.Equals(thread, "all", StringComparison.InvariantCultureIgnoreCase))
            {
                return Environment.ProcessorCount;
            }
            if (int.TryParse(thread, out int nbThread))
            {
                return nbThread;
            }
            throw new CommandLineException("Maximum threads need to be either numeric, or \"all\" ");
        }


        public static void ExtractAsymetricFile(TccOption option, Mode mode, string passwordKey)
        {
            if (option.PasswordOption != NoPasswordOption.Nop)
                throw new CommandLineException("Only one password mode allowed");

            if (string.IsNullOrEmpty(passwordKey))
                throw new CommandLineException("Public or private key must be specified");

            if (!File.Exists(passwordKey))
                throw new CommandLineException("Public or private key file doesn't exists");

            option.PasswordOption = mode switch
            {
                Mode.Compress => new PublicKeyPasswordOption { PublicKeyFile = passwordKey },
                Mode.Decompress => new PrivateKeyPasswordOption { PrivateKeyFile = passwordKey },
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
        }

        public static void ExtractPasswordFile(TccOption option, string passwordFile)
        {
            if (option.PasswordOption != NoPasswordOption.Nop)
                throw new CommandLineException("Only one password mode allowed");

            if (string.IsNullOrEmpty(passwordFile))
                throw new CommandLineException("Password file must be specified");

            if (!File.Exists(passwordFile))
                throw new CommandLineException("Password file doesn't exists");

            option.PasswordOption = new PasswordFileOption
            {
                PasswordFile = passwordFile
            };
        }

        public static void ExtractInlinePassword(TccOption option, string password)
        {
            if (option.PasswordOption != NoPasswordOption.Nop)
                throw new CommandLineException("Only one password mode allowed");

            if (string.IsNullOrEmpty(password))
                throw new CommandLineException("Password must be specified");

            option.PasswordOption = new InlinePasswordOption
            {
                Password = password
            };
        }
    }
}