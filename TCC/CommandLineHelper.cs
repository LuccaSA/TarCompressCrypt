using System;
using System.Linq;

namespace TCC
{
    public static class CommandLineHelper
    {
        public static TccOption ProcessCommandLine(this string[] args, out Mode mode)
        {
            var option = args.ParseCommandLine(out Mode compressMode);
            mode = compressMode;
            if (option != null && mode != Mode.Unknown)
            {
                return option;
            }
            switch (mode)
            {
                case Mode.Unknown:
                    PrintHelp();
                    break;
                case Mode.Compress:
                    PrintCompressHelp();
                    break;
                case Mode.Decompress:
                    PrintDecompressHelp();
                    break;
            }
            Environment.Exit(1);
            return option;
        }

        private static TccOption ParseCommandLine(this string[] args, out Mode mode)
        {
            if (args.Length == 0)
            {
                mode = Mode.Unknown;
                return null;
            }

            // arg 1 : verb
            TccOption option;
            if (string.Equals("compress", args[0], StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals("c", args[0], StringComparison.InvariantCultureIgnoreCase))
            {
                mode = Mode.Compress;
                var compressOption = new CompressOption();
                option = compressOption;
            }
            else if (string.Equals("decompress", args[0], StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals("d", args[0], StringComparison.InvariantCultureIgnoreCase))
            {
                mode = Mode.Decompress;
                option = new DecompressOption();
            }
            else
            {
                mode = Mode.Unknown;
                return null;
            }

            if (args.Length == 1)
            {
                // no path
                return null;
            }

            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-i":
                        {
                            if (option is CompressOption co)
                            {
                                if (!args.TryGetNext(i, out string data))
                                    return null;
                                if (!Enum.TryParse(data, out BlockMode blockMode))
                                    return null;
                                co.BlockMode = blockMode;
                                i++;
                            }
                            else
                            {
                                throw new InvalidOperationException("BlockMode invalid in decompress mode");
                            }
                            break;
                        }
                    case "-o":
                        {
                            if (!args.TryGetNext(i, out string data))
                                return null;
                            option.DestinationDir = data;
                            i++;
                            break;
                        }
                    case "-p":
                        {
                            if (!args.TryGetNext(i, out string data) || option.PasswordMode != PasswordMode.None)
                                return null;
                            option.Password = data;
                            option.PasswordMode = PasswordMode.InlinePassword;
                            i++;
                            break;
                        }
                    case "-e":
                        {
                            if (!args.TryGetNext(i, out string data) || option.PasswordMode != PasswordMode.None)
                                return null;
                            option.PasswordFile = data;
                            option.PasswordMode = PasswordMode.PasswordFile;
                            i++;
                            break;
                        }
                    case "-k":
                        {
                            if (!args.TryGetNext(i, out string data) || option.PasswordMode != PasswordMode.None)
                                return null;
                            option.PublicPrivateKeyFile = data;
                            option.PasswordMode = PasswordMode.PublicKey;
                            i++;
                            break;
                        }
                    case "-t":
                        {
                            if (!args.TryGetNext(i, out string data))
                                return null;
                            option.Threads = data;
                            i++;
                            break;
                        }
                    case "-f":
                        {
                            option.FailFast = true;
                            break;
                        }
                    default:
                        {
                            if (i != 1)
                            {
                                // first argument is not the path
                                return null;
                            }
                            option.SourceDirOrFile = args[1];
                            break;
                        }
                }
            }
            return option;
        }

        public static void PrintWarningPassword()
        {
            // Only one password method at the same time ;)
        }

        public static void PrintHelp()
        {
            string message = @"
Usage : ttc.exe compress/decompress source [options]

-o Destination    : Directory directory path
-t threads        : [1,2...N] / all
-f                : fail fast mode
-i                : individual mode (only on compression mode)
                    create distinct archives fore ach file / folder in source
-p password       : encryption password
-e passFile       : file with password on one line
-k asymetric key  : public key for compression, private key for decompression
";
            Console.WriteLine(message);
        }

        public static void PrintCompressHelp()
        {
        }

        public static void PrintDecompressHelp()
        {
        }

        public static bool TryGetNext(this string[] args, int index, out string data)
        {
            if (index >= args.Length)
            {
                data = null;
                return false;
            }
            if (string.IsNullOrWhiteSpace(args[index + 1]))
            {
                data = null;
                return false;
            }
            data = args[index + 1];
            return true;
        }

    }

}
